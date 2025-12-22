using System.Collections.Concurrent;
using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Immutable KD-tree supporting lock-free concurrent reads.
/// Tree is rebuilt entirely on updates (copy-on-write semantics).
/// </summary>
public sealed class ImmutableKDTree
{
    /// <summary>
    /// The version of the file format that this class writes.
    /// </summary>
    private const int CurrentFileVersion = 2;

    private readonly IDistanceCalculator _distanceCalculator;

    /// <summary>
    /// The root node. Null if tree is empty.
    /// </summary>
    public ImmutableKDTreeNode? Root { get; }

    /// <summary>
    /// Creates an empty immutable KD-tree.
    /// </summary>
    public ImmutableKDTree()
        : this(null, EuclideanDistanceCalculator.Instance)
    {
    }

    private ImmutableKDTree(ImmutableKDTreeNode? root, IDistanceCalculator distanceCalculator)
    {
        Root = root;
        _distanceCalculator = distanceCalculator;
    }

    /// <summary>
    /// Builds a new immutable KD-tree from vectors.
    /// </summary>
    /// <param name="vectors">The vectors to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new immutable KD-tree containing all vectors.</returns>
    public static async Task<ImmutableKDTree> BuildAsync(
        VectorList vectors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);

        if (vectors.Count == 0)
        {
            return new ImmutableKDTree();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var vectorList = vectors.ToList();
        bool useParallel = KDTreeParallelConfig.EnableParallelConstruction &&
                           vectorList.Count >= KDTreeParallelConfig.ParallelConstructionThreshold;

        var root = useParallel
            ? await BuildParallelAsync(vectorList, 0, cancellationToken).ConfigureAwait(false)
            : BuildSequential(vectorList, 0, cancellationToken);

        return new ImmutableKDTree(root, EuclideanDistanceCalculator.Instance);
    }

    private static ImmutableKDTreeNode? BuildSequential(
        IList<Vector> vectors,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (vectors.Count == 0)
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

        return new ImmutableKDTreeNode
        {
            Vector = sortedVectors[median],
            Left = BuildSequential(sortedVectors.Take(median).ToList(), depth + 1, cancellationToken),
            Right = BuildSequential(sortedVectors.Skip(median + 1).ToList(), depth + 1, cancellationToken)
        };
    }

    private static async Task<ImmutableKDTreeNode?> BuildParallelAsync(
        IList<Vector> vectors,
        int depth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (vectors.Count == 0)
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

        cancellationToken.ThrowIfCancellationRequested();

        var median = sortedVectors.Count / 2;
        var leftVectors = sortedVectors.Take(median).ToList();
        var rightVectors = sortedVectors.Skip(median + 1).ToList();

        Task<ImmutableKDTreeNode?> leftTask;
        Task<ImmutableKDTreeNode?> rightTask;

        if (leftVectors.Count >= KDTreeParallelConfig.MinParallelSubtreeSize)
        {
            leftTask = BuildParallelAsync(leftVectors, depth + 1, cancellationToken);
        }
        else
        {
            leftTask = Task.FromResult(BuildSequential(leftVectors, depth + 1, cancellationToken));
        }

        if (rightVectors.Count >= KDTreeParallelConfig.MinParallelSubtreeSize)
        {
            rightTask = BuildParallelAsync(rightVectors, depth + 1, cancellationToken);
        }
        else
        {
            rightTask = Task.FromResult(BuildSequential(rightVectors, depth + 1, cancellationToken));
        }

        await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);

        return new ImmutableKDTreeNode
        {
            Vector = sortedVectors[median],
            Left = leftTask.Result,
            Right = rightTask.Result
        };
    }

    /// <summary>
    /// Finds the k nearest neighbors to the query vector.
    /// This method is lock-free and can be called concurrently.
    /// </summary>
    /// <param name="query">The query vector.</param>
    /// <param name="k">The number of neighbors to find.</param>
    /// <returns>The k nearest neighbors, ordered by distance.</returns>
    public IList<Vector> NearestNeighbors(Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        // Capture root reference once - atomic read for lock-free access
        var root = Root;
        if (root is null)
        {
            return Array.Empty<Vector>();
        }

        var candidates = new BoundedPriorityQueue(k);
        SearchNearestNeighbors(root, query, k, 0, candidates);

        return candidates.GetResults();
    }

    private void SearchNearestNeighbors(
        ImmutableKDTreeNode? node,
        Vector query,
        int k,
        int depth,
        BoundedPriorityQueue candidates)
    {
        if (node is null)
        {
            return;
        }

        // Add current node to candidates
        var distance = _distanceCalculator.CalculateDistance(query, node.Vector);
        candidates.TryAdd(node.Vector, distance);

        if (node.IsLeaf)
        {
            return;
        }

        var axis = depth % query.Dimensions;
        var queryAxisValue = query.Values[axis];
        var nodeAxisValue = node.Vector.Values[axis];

        var nearChild = queryAxisValue < nodeAxisValue ? node.Left : node.Right;
        var farChild = queryAxisValue < nodeAxisValue ? node.Right : node.Left;

        // Always search the near child
        SearchNearestNeighbors(nearChild, query, k, depth + 1, candidates);

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(queryAxisValue - nodeAxisValue);
        if (!candidates.IsFull || axisDistance < candidates.WorstDistance)
        {
            SearchNearestNeighbors(farChild, query, k, depth + 1, candidates);
        }
    }

    /// <summary>
    /// Finds all vectors within a specified radius of the query vector.
    /// This method is lock-free and can be called concurrently.
    /// </summary>
    /// <param name="query">The query vector.</param>
    /// <param name="radius">The maximum distance from the query.</param>
    /// <param name="distanceCalculator">The distance calculator to use (defaults to Euclidean).</param>
    /// <returns>Vectors within the specified radius, ordered by distance.</returns>
    public IList<Vector> RangeNeighbors(Vector query, float radius, IDistanceCalculator? distanceCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
        }

        distanceCalculator ??= EuclideanDistanceCalculator.Instance;

        // Capture root reference once - atomic read for lock-free access
        var root = Root;
        if (root is null)
        {
            return Array.Empty<Vector>();
        }

        var results = new List<(Vector vector, float distance)>();
        SearchRangeNeighbors(root, query, radius, 0, distanceCalculator, results);

        // Sort by distance, then by vector ID for consistent ordering
        return results
            .OrderBy(r => r.distance)
            .ThenBy(r => r.vector.Id)
            .Select(r => r.vector)
            .ToList();
    }

    private void SearchRangeNeighbors(
        ImmutableKDTreeNode? node,
        Vector query,
        float radius,
        int depth,
        IDistanceCalculator distanceCalculator,
        List<(Vector vector, float distance)> results)
    {
        if (node is null)
        {
            return;
        }

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
        SearchRangeNeighbors(nearChild, query, radius, depth + 1, distanceCalculator, results);

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);
        if (axisDistance <= radius)
        {
            SearchRangeNeighbors(farChild, query, radius, depth + 1, distanceCalculator, results);
        }
    }

    /// <summary>
    /// Saves the tree to a binary writer.
    /// </summary>
    public void Save(BinaryWriter writer, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(vectors);

        writer.Write(CurrentFileVersion);
        Root?.WriteTo(writer);
    }

    /// <summary>
    /// Loads an immutable KD-tree from a binary reader.
    /// Supports both legacy (v1) and immutable (v2) formats.
    /// </summary>
    public static ImmutableKDTree Load(BinaryReader reader, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(vectors);

        var version = reader.ReadInt32();
        if (version != 1 && version != CurrentFileVersion)
        {
            throw new InvalidDataException($"Invalid KD tree version: {version}");
        }

        Span<byte> guidBuffer = stackalloc byte[16];
        var root = ImmutableKDTreeNode.ReadFrom(reader, vectors, guidBuffer);

        return new ImmutableKDTree(root, EuclideanDistanceCalculator.Instance);
    }

    /// <summary>
    /// Bounded priority queue for k-NN search.
    /// Uses a max-heap to keep the worst element at the top for easy removal.
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
}
