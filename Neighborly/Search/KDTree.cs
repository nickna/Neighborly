using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Configuration options for KDTree parallel processing
/// </summary>
public static class KDTreeParallelConfig
{
    /// <summary>
    /// Minimum dataset size to enable parallel tree construction
    /// </summary>
    public static int ParallelConstructionThreshold { get; set; } = 1000;

    /// <summary>
    /// Minimum subtree size to continue parallel processing during construction
    /// </summary>
    public static int MinParallelSubtreeSize { get; set; } = 100;

    /// <summary>
    /// Maximum depth for parallel search operations (k-NN and range)
    /// Beyond this depth, operations become sequential to avoid task overhead
    /// </summary>
    public static int MaxParallelSearchDepth { get; set; } = 4;

    /// <summary>
    /// Enable or disable parallel tree construction globally
    /// </summary>
    public static bool EnableParallelConstruction { get; set; } = true;

    /// <summary>
    /// Enable or disable parallel search operations globally
    /// </summary>
    public static bool EnableParallelSearch { get; set; } = true;
}

/// <summary>
/// K-D Tree search (see Wikipedia: https://en.wikipedia.org/wiki/K-d_tree)
/// </summary>
public class KDTree
{
    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

    private KDTreeNode? root;

    /// <summary>
    /// Bounded priority queue that efficiently maintains the k best candidates
    /// Uses a max-heap to keep the worst element at the top for easy removal
    /// </summary>
    private sealed class BoundedPriorityQueue
    {
        private readonly int _capacity;
        private readonly PriorityQueue<Vector, float> _heap;

        public BoundedPriorityQueue(int capacity)
        {
            _capacity = capacity;
            _heap = new PriorityQueue<Vector, float>();
        }

        public bool IsFull => _heap.Count >= _capacity;

        public float WorstDistance
        {
            get
            {
                if (_heap.Count == 0)
                    return float.MaxValue;
                
                _heap.TryPeek(out _, out var priority);
                return -priority; // Convert back from negated value
            }
        }

        public void TryAdd(Vector vector, float distance)
        {
            if (_heap.Count < _capacity)
            {
                // Use negative distance to create max-heap behavior (worst at top)
                _heap.Enqueue(vector, -distance);
            }
            else if (_heap.TryPeek(out _, out var worstPriority) && distance < -worstPriority)
            {
                _heap.Dequeue(); // Remove worst
                _heap.Enqueue(vector, -distance);
            }
        }

        public IList<Vector> GetResults()
        {
            var results = new List<Vector>(_heap.Count);
            var tempQueue = new PriorityQueue<Vector, float>();
            
            // Extract all elements and re-negate to get correct distances for sorting
            while (_heap.TryDequeue(out var vector, out var priority))
            {
                tempQueue.Enqueue(vector, priority); // Keep negative for min-heap ordering
            }
            
            // Dequeue in ascending distance order (best first)
            while (tempQueue.TryDequeue(out var vector, out _))
            {
                results.Add(vector);
            }
            
            return results;
        }
    }

    /// <summary>
    /// Thread-safe bounded priority queue for parallel k-NN search operations
    /// Uses concurrent data structures and locking for thread safety
    /// </summary>
    private sealed class ThreadSafeBoundedPriorityQueue
    {
        private readonly int _capacity;
        private readonly ConcurrentQueue<(Vector vector, float distance)> _candidates;
        private readonly object _lock = new object();
        private volatile int _count = 0;

        public ThreadSafeBoundedPriorityQueue(int capacity)
        {
            _capacity = capacity;
            _candidates = new ConcurrentQueue<(Vector, float)>();
        }

        public bool IsFull => _count >= _capacity;

        public float WorstDistance
        {
            get
            {
                lock (_lock)
                {
                    if (_count == 0) return float.MaxValue;
                    
                    var items = _candidates.ToArray();
                    if (items.Length == 0) return float.MaxValue;
                    
                    return items.Max(item => item.distance);
                }
            }
        }

        public void TryAdd(Vector vector, float distance)
        {
            lock (_lock)
            {
                _candidates.Enqueue((vector, distance));
                _count++;

                // If we exceed capacity, remove the worst element
                if (_count > _capacity)
                {
                    var items = new List<(Vector vector, float distance)>();
                    
                    // Drain the queue
                    while (_candidates.TryDequeue(out var item))
                    {
                        items.Add(item);
                    }

                    // Sort by distance and keep only the best k
                    items.Sort((a, b) => a.distance.CompareTo(b.distance));
                    items = items.Take(_capacity).ToList();

                    // Re-enqueue the best items
                    foreach (var item in items)
                    {
                        _candidates.Enqueue(item);
                    }

                    _count = items.Count;
                }
            }
        }

        public IList<Vector> GetResults()
        {
            lock (_lock)
            {
                var items = _candidates.ToArray();
                Array.Sort(items, (a, b) => a.distance.CompareTo(b.distance));
                return items.Select(item => item.vector).ToList();
            }
        }
    }

    public void Build(VectorList vectors)
    {
        if (vectors == null)
        {
            throw new ArgumentNullException(nameof(vectors), "Vector list cannot be null");
        }
        if (vectors.Count == 0)
        {
            return;
        }

        // Use parallel construction for large datasets if enabled
        bool useParallel = KDTreeParallelConfig.EnableParallelConstruction && 
                          vectors.Count >= KDTreeParallelConfig.ParallelConstructionThreshold;
        root = useParallel ? BuildParallel(vectors, 0) : Build(vectors, 0);
    }

    public void Load(BinaryReader reader, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(vectors);

        var version = reader.ReadInt32(); // Read the version number
        if (version != s_currentFileVersion)
        {
            throw new InvalidDataException($"Invalid KD tree version: {version}");
        }

        root = null;
        Span<byte> guidBuffer = stackalloc byte[16];
        // Read the tree starting at the root node
        root = KDTreeNode.ReadFrom(reader, vectors, guidBuffer);
    }

    public void Save(BinaryWriter writer, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(vectors);

        writer.Write(s_currentFileVersion); // Write the version number

        root?.WriteTo(writer);
    }

    private KDTreeNode? Build(IList<Vector> vectors, int depth)
    {
        if (vectors.Count <= 0)
        {
            return null;
        }

        var firstVector = vectors[0];
        if (firstVector.Dimensions == 0)
        {
            return null;
        }

        var axis = depth % firstVector.Dimensions;
        var sortedVectors = vectors.OrderBy(v => v[axis]).ToList();

        var median = sortedVectors.Count / 2;

        return new KDTreeNode
        {
            Vector = sortedVectors[median],
            Left = Build(sortedVectors.Take(median).ToList(), depth + 1),
            Right = Build(sortedVectors.Skip(median + 1).ToList(), depth + 1)
        };
    }

    private KDTreeNode? BuildParallel(IList<Vector> vectors, int depth)
    {
        if (vectors.Count <= 0)
        {
            return null;
        }

        var firstVector = vectors[0];
        if (firstVector.Dimensions == 0)
        {
            return null;
        }

        var axis = depth % firstVector.Dimensions;
        var sortedVectors = vectors.OrderBy(v => v[axis]).ToList();

        var median = sortedVectors.Count / 2;
        var leftVectors = sortedVectors.Take(median).ToList();
        var rightVectors = sortedVectors.Skip(median + 1).ToList();

        // Use parallel construction for large subtrees, sequential for small ones
        KDTreeNode? leftChild = null;
        KDTreeNode? rightChild = null;

        if (leftVectors.Count >= KDTreeParallelConfig.MinParallelSubtreeSize || rightVectors.Count >= KDTreeParallelConfig.MinParallelSubtreeSize)
        {
            // Parallel construction for larger subtrees
            var leftTask = Task.Run(() => leftVectors.Count >= KDTreeParallelConfig.MinParallelSubtreeSize 
                ? BuildParallel(leftVectors, depth + 1) 
                : Build(leftVectors, depth + 1));
            
            var rightTask = Task.Run(() => rightVectors.Count >= KDTreeParallelConfig.MinParallelSubtreeSize 
                ? BuildParallel(rightVectors, depth + 1) 
                : Build(rightVectors, depth + 1));

            Task.WaitAll(leftTask, rightTask);
            leftChild = leftTask.Result;
            rightChild = rightTask.Result;
        }
        else
        {
            // Sequential construction for smaller subtrees to avoid overhead
            leftChild = Build(leftVectors, depth + 1);
            rightChild = Build(rightVectors, depth + 1);
        }

        return new KDTreeNode
        {
            Vector = sortedVectors[median],
            Left = leftChild,
            Right = rightChild
        };
    }

    public IList<Vector> NearestNeighbors(Vector query, int k)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
        }
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        var candidates = new BoundedPriorityQueue(k);
        NearestNeighbors(root, query, k, 0, candidates);
        
        return candidates.GetResults();
    }

    /// <summary>
    /// Parallel version of nearest neighbors search for large datasets
    /// Uses multiple threads to search different subtrees concurrently
    /// </summary>
    public IList<Vector> NearestNeighborsParallel(Vector query, int k)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
        }
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        var candidates = new ThreadSafeBoundedPriorityQueue(k);
        NearestNeighborsParallel(root, query, k, 0, candidates);
        
        return candidates.GetResults();
    }

    /// <summary>
    /// Finds all vectors within a specified radius of the query vector using the KD-tree structure.
    /// </summary>
    /// <param name="query">The query vector</param>
    /// <param name="radius">The maximum distance from the query</param>
    /// <param name="distanceCalculator">The distance calculator to use</param>
    /// <returns>A list of vectors within the specified radius, ordered by distance</returns>
    public IList<Vector> RangeNeighbors(Vector query, float radius, IDistanceCalculator? distanceCalculator = null)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
        }
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
        }

        distanceCalculator ??= EuclideanDistanceCalculator.Instance;
        var results = new List<(Vector vector, float distance)>();
        RangeNeighbors(root, query, radius, 0, distanceCalculator, results);
        
        // Sort by distance and return just the vectors
        return results
            .OrderBy(r => r.distance)
            .Select(r => r.vector)
            .ToList();
    }

    /// <summary>
    /// Parallel version of range neighbors search for large datasets
    /// Uses concurrent collections to safely collect results from multiple threads
    /// </summary>
    public IList<Vector> RangeNeighborsParallel(Vector query, float radius, IDistanceCalculator? distanceCalculator = null)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
        }
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
        }

        distanceCalculator ??= EuclideanDistanceCalculator.Instance;
        var results = new ConcurrentBag<(Vector vector, float distance)>();
        RangeNeighborsParallel(root, query, radius, 0, distanceCalculator, results);
        
        // Sort by distance and return just the vectors
        return results
            .OrderBy(r => r.distance)
            .Select(r => r.vector)
            .ToList();
    }

    private void RangeNeighbors(KDTreeNode? node, Vector query, float radius, int depth, IDistanceCalculator distanceCalculator, List<(Vector vector, float distance)> results)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimensions;
        var distance = distanceCalculator.CalculateDistance(node.Vector, query);
        
        // Add current node if it's within radius
        if (distance <= radius)
        {
            results.Add((node.Vector, distance));
        }

        // Determine which child to search first
        var nearChild = query[axis] <= node.Vector[axis] ? node.Left : node.Right;
        var farChild = query[axis] <= node.Vector[axis] ? node.Right : node.Left;

        // Always search the near child
        RangeNeighbors(nearChild, query, radius, depth + 1, distanceCalculator, results);

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);
        if (axisDistance <= radius)
        {
            RangeNeighbors(farChild, query, radius, depth + 1, distanceCalculator, results);
        }
    }

    private void RangeNeighborsParallel(KDTreeNode? node, Vector query, float radius, int depth, IDistanceCalculator distanceCalculator, ConcurrentBag<(Vector vector, float distance)> results)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimensions;
        var distance = distanceCalculator.CalculateDistance(node.Vector, query);
        
        // Add current node if it's within radius
        if (distance <= radius)
        {
            results.Add((node.Vector, distance));
        }

        // Determine which child to search first
        var nearChild = query[axis] <= node.Vector[axis] ? node.Left : node.Right;
        var farChild = query[axis] <= node.Vector[axis] ? node.Right : node.Left;

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);

        if (depth <= KDTreeParallelConfig.MaxParallelSearchDepth) // Use parallel execution for shallow depths
        {
            // Create tasks for parallel execution
            var tasks = new List<Task>();

            // Always search the near child
            if (nearChild != null)
            {
                tasks.Add(Task.Run(() => RangeNeighborsParallel(nearChild, query, radius, depth + 1, distanceCalculator, results)));
            }

            // Search far child if within radius
            if (farChild != null && axisDistance <= radius)
            {
                tasks.Add(Task.Run(() => RangeNeighborsParallel(farChild, query, radius, depth + 1, distanceCalculator, results)));
            }

            // Wait for all tasks to complete
            Task.WaitAll(tasks.ToArray());
        }
        else
        {
            // Use sequential execution for deeper levels to avoid task overhead
            RangeNeighborsParallel(nearChild, query, radius, depth + 1, distanceCalculator, results);

            if (axisDistance <= radius)
            {
                RangeNeighborsParallel(farChild, query, radius, depth + 1, distanceCalculator, results);
            }
        }
    }

    private void NearestNeighbors(KDTreeNode? node, Vector query, int k, int depth, BoundedPriorityQueue candidates)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimensions;
        var distance = (node.Vector - query).Magnitude;
        
        // Add current node to candidates
        candidates.TryAdd(node.Vector, distance);

        // Determine which child to search first
        var nearChild = query[axis] <= node.Vector[axis] ? node.Left : node.Right;
        var farChild = query[axis] <= node.Vector[axis] ? node.Right : node.Left;

        // Search the near child first
        NearestNeighbors(nearChild, query, k, depth + 1, candidates);

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);
        if (!candidates.IsFull || axisDistance < candidates.WorstDistance)
        {
            NearestNeighbors(farChild, query, k, depth + 1, candidates);
        }
    }

    private void NearestNeighborsParallel(KDTreeNode? node, Vector query, int k, int depth, ThreadSafeBoundedPriorityQueue candidates)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimensions;
        var distance = (node.Vector - query).Magnitude;
        
        // Add current node to candidates
        candidates.TryAdd(node.Vector, distance);

        // Determine which child to search first
        var nearChild = query[axis] <= node.Vector[axis] ? node.Left : node.Right;
        var farChild = query[axis] <= node.Vector[axis] ? node.Right : node.Left;

        // Search the near child first (always)
        NearestNeighborsParallel(nearChild, query, k, depth + 1, candidates);

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);
        if (!candidates.IsFull || axisDistance < candidates.WorstDistance)
        {
            // For shallow levels, use parallel execution to search the far child
            if (depth <= KDTreeParallelConfig.MaxParallelSearchDepth) // Only parallelize at shallow depths to avoid excessive task overhead
            {
                var task = Task.Run(() => NearestNeighborsParallel(farChild, query, k, depth + 1, candidates));
                task.Wait(); // Wait for completion to ensure consistent results
            }
            else
            {
                // Use sequential search for deeper levels
                NearestNeighborsParallel(farChild, query, k, depth + 1, candidates);
            }
        }
    }

    public IList<Vector> Search(Vector query, int k)
    {
        // Perform the nearest neighbor search
        var results = NearestNeighbors(query, k);

        return results;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not KDTree other)
        {
            return false;
        }

        return Equals(root, other.root);
    }

    public override int GetHashCode()
    {
        return root?.GetHashCode() ?? 0;
    }

}
