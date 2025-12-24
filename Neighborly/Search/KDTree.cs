using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neighborly.Distance;
using Neighborly.Search.Configuration;
using Neighborly.Search.Utilities;

namespace Neighborly.Search;

/// <summary>
/// K-D Tree search (see Wikipedia: https://en.wikipedia.org/wiki/K-d_tree)
/// </summary>
public class KDTree : ISerializableSearchIndex
{
    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

    private KDTreeNode? root;
    private readonly IDistanceCalculator _distanceCalculator;
    private readonly KDTreeConfiguration _configuration;

    /// <summary>
    /// Returns true if the index has been built and is ready for searches.
    /// </summary>
    public bool IsBuilt => root != null;

    /// <summary>
    /// The file format version this implementation writes.
    /// </summary>
    public int FileFormatVersion => s_currentFileVersion;

    /// <summary>
    /// Gets the configuration for this KDTree instance.
    /// </summary>
    public KDTreeConfiguration Configuration => _configuration;

    public KDTree(KDTreeConfiguration? configuration = null)
    {
        _distanceCalculator = EuclideanDistanceCalculator.Instance;
        _configuration = configuration ?? KDTreeConfiguration.Default;
        _configuration.Validate();
    }

    public async Task BuildAsync(VectorList vectors, CancellationToken cancellationToken = default)
    {
        if (vectors == null)
        {
            throw new ArgumentNullException(nameof(vectors), "Vector list cannot be null");
        }
        if (vectors.Count == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Use parallel construction for large datasets if enabled
        bool useParallel = _configuration.EnableParallelConstruction &&
                          vectors.Count >= _configuration.ParallelConstructionThreshold;
        root = useParallel ? await BuildParallel(vectors, 0, cancellationToken).ConfigureAwait(false) : Build(vectors, 0, cancellationToken);
    }

    public Task LoadAsync(BinaryReader reader, VectorList vectors, CancellationToken cancellationToken = default)
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

        return Task.CompletedTask;
    }

    public Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(s_currentFileVersion); // Write the version number

        root?.WriteTo(writer);

        return Task.CompletedTask;
    }

    private KDTreeNode? Build(IList<Vector> vectors, int depth, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (vectors.Count <= 0)
        {
            return null;
        }

        var firstVector = vectors[0];
        if (firstVector.Dimension == 0)
        {
            return null;
        }

        var axis = depth % firstVector.Dimension;
        var sortedVectors = vectors.OrderBy(v => v[axis]).ToList();

        var median = sortedVectors.Count / 2;

        return new KDTreeNode
        {
            Vector = sortedVectors[median],
            Left = Build(sortedVectors.Take(median).ToList(), depth + 1, cancellationToken),
            Right = Build(sortedVectors.Skip(median + 1).ToList(), depth + 1, cancellationToken)
        };
    }

    private async Task<KDTreeNode?> BuildParallel(IList<Vector> vectors, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (vectors.Count <= 0)
        {
            return null;
        }

        var firstVector = vectors[0];
        if (firstVector.Dimension == 0)
        {
            return null;
        }

        var axis = depth % firstVector.Dimension;
        var sortedVectors = vectors.OrderBy(v => v[axis]).ToList();

        cancellationToken.ThrowIfCancellationRequested();

        var median = sortedVectors.Count / 2;
        var leftVectors = sortedVectors.Take(median).ToList();
        var rightVectors = sortedVectors.Skip(median + 1).ToList();

        Task<KDTreeNode?> leftChildTask;
        Task<KDTreeNode?> rightChildTask;

        if (leftVectors.Count >= _configuration.MinParallelSubtreeSize)
        {
            leftChildTask = BuildParallel(leftVectors, depth + 1, cancellationToken);
        }
        else
        {
            leftChildTask = Task.FromResult(Build(leftVectors, depth + 1, cancellationToken));
        }

        if (rightVectors.Count >= _configuration.MinParallelSubtreeSize)
        {
            rightChildTask = BuildParallel(rightVectors, depth + 1, cancellationToken);
        }
        else
        {
            rightChildTask = Task.FromResult(Build(rightVectors, depth + 1, cancellationToken));
        }

        await Task.WhenAll(leftChildTask, rightChildTask).ConfigureAwait(false);

        return new KDTreeNode
        {
            Vector = sortedVectors[median],
            Left = leftChildTask.Result,
            Right = rightChildTask.Result
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

        var candidates = new ThreadSafeBoundedPriorityQueue<Vector>(k);
        NearestNeighbors(root, query, k, 0, candidates);

        return candidates.GetResults();
    }

    /// <summary>
    /// Parallel version of nearest neighbors search for large datasets
    /// Uses multiple threads to search different subtrees concurrently
    /// </summary>
    public async Task<IList<Vector>> NearestNeighborsParallel(Vector query, int k)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
        }
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        var candidates = new ThreadSafeBoundedPriorityQueue<Vector>(k);
        await NearestNeighborsParallel(root, query, k, 0, candidates).ConfigureAwait(false);

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
        
        // Sort by distance, then by vector ID for consistent ordering when distances are equal
        return results
            .OrderBy(r => r.distance)
            .ThenBy(r => r.vector.Id)
            .Select(r => r.vector)
            .ToList();
    }

    /// <summary>
    /// Parallel version of range neighbors search for large datasets
    /// Uses concurrent collections to safely collect results from multiple threads
    /// </summary>
    public async Task<IList<Vector>> RangeNeighborsParallel(Vector query, float radius, IDistanceCalculator? distanceCalculator = null)
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
        await RangeNeighborsParallel(root, query, radius, 0, distanceCalculator, results).ConfigureAwait(false);

        // Sort by distance, then by vector ID for consistent ordering when distances are equal
        return results
            .OrderBy(r => r.distance)
            .ThenBy(r => r.vector.Id)
            .Select(r => r.vector)
            .ToList();
    }

    private void RangeNeighbors(KDTreeNode? node, Vector query, float radius, int depth, IDistanceCalculator distanceCalculator, System.Collections.Generic.List<(Vector vector, float distance)> results)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimension;
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

    private async Task RangeNeighborsParallel(KDTreeNode? node, Vector query, float radius, int depth, IDistanceCalculator distanceCalculator, System.Collections.Concurrent.ConcurrentBag<(Vector vector, float distance)> results)
    {
        if (node == null)
        {
            return;
        }

        // Check and add current node if it's within radius (both leaf and internal nodes)
        float distance = distanceCalculator.CalculateDistance(query, node.Vector);
        if (distance <= radius)
        {
            results.Add((node.Vector, distance));
        }
        
        if (node.IsLeaf)
        {
            return;
        }

        int axis = depth % query.Dimension;
        float queryAxisValue = query.Values[axis];

        KDTreeNode? nearChild = queryAxisValue < node.Vector.Values[axis] ? node.Left : node.Right;
        KDTreeNode? farChild = queryAxisValue < node.Vector.Values[axis] ? node.Right : node.Left;

        float axisDistance = Math.Abs(queryAxisValue - node.Vector.Values[axis]);

        if (depth < _configuration.MaxParallelSearchDepth)
        {
            List<Task> tasks = new();
            tasks.Add(RangeNeighborsParallel(nearChild, query, radius, depth + 1, distanceCalculator, results));

            if (axisDistance <= radius)
            {
                tasks.Add(RangeNeighborsParallel(farChild, query, radius, depth + 1, distanceCalculator, results));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        else
        {
            // Use sequential execution for deeper levels to avoid task overhead
            await RangeNeighborsParallel(nearChild, query, radius, depth + 1, distanceCalculator, results).ConfigureAwait(false);

            if (axisDistance <= radius)
            {
                await RangeNeighborsParallel(farChild, query, radius, depth + 1, distanceCalculator, results).ConfigureAwait(false);
            }
        }
    }

    private void NearestNeighbors(KDTreeNode? node, Vector query, int k, int depth, ThreadSafeBoundedPriorityQueue<Vector> candidates)
    {
        if (node == null)
        {
            return;
        }

        // Add current node to candidates (both leaf and internal nodes)
        candidates.TryAdd(node.Vector, _distanceCalculator.CalculateDistance(query, node.Vector));
        
        if (node.IsLeaf)
        {
            return;
        }

        int axis = depth % query.Dimension;
        float queryAxisValue = query.Values[axis];

        KDTreeNode? nearChild = queryAxisValue < node.Vector.Values[axis] ? node.Left : node.Right;
        KDTreeNode? farChild = queryAxisValue < node.Vector.Values[axis] ? node.Right : node.Left;

        NearestNeighbors(nearChild, query, k, depth + 1, candidates);

        float axisDistance = Math.Abs(queryAxisValue - node.Vector.Values[axis]);

        if (candidates.Count < k || axisDistance < candidates.WorstDistance)
        {
            NearestNeighbors(farChild, query, k, depth + 1, candidates);
        }
    }

    private async Task NearestNeighborsParallel(KDTreeNode? node, Vector query, int k, int depth, ThreadSafeBoundedPriorityQueue<Vector> candidates)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimension;
        var distance = _distanceCalculator.CalculateDistance(query, node.Vector);
        
        // Add current node to candidates
        candidates.TryAdd(node.Vector, distance);

        // Determine which child to search first
        var nearChild = query[axis] <= node.Vector[axis] ? node.Left : node.Right;
        var farChild = query[axis] <= node.Vector[axis] ? node.Right : node.Left;

        // Create a list of tasks to await
        var tasks = new List<Task>();

        // Search the near child first (always)
        tasks.Add(NearestNeighborsParallel(nearChild, query, k, depth + 1, candidates));

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);
        if (!candidates.IsFull || axisDistance < candidates.WorstDistance)
        {
            // For shallow levels, use parallel execution to search the far child
            if (depth <= _configuration.MaxParallelSearchDepth)
            {
                tasks.Add(NearestNeighborsParallel(farChild, query, k, depth + 1, candidates));
            }
            else
            {
                // Use sequential search for deeper levels
                NearestNeighbors(farChild, query, k, depth + 1, candidates);
            }
        }

        await Task.WhenAll(tasks);
    }

    public IList<Vector> Search(Vector query, int k)
    {
        // Perform the nearest neighbor search
        var results = NearestNeighbors(query, k);

        return results;
    }

    public async Task<IList<Vector>> SearchParallel(Vector query, int k)
    {
        // Perform the nearest neighbor search in parallel
        var results = await NearestNeighborsParallel(query, k).ConfigureAwait(false);

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
