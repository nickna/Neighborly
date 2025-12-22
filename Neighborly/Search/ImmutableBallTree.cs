namespace Neighborly.Search;

/// <summary>
/// Immutable Ball-tree supporting lock-free concurrent reads.
/// Tree is rebuilt entirely on updates (copy-on-write semantics).
/// </summary>
public sealed class ImmutableBallTree
{
    /// <summary>
    /// The version of the file format that this class writes.
    /// </summary>
    private const int CurrentFileVersion = 2;

    /// <summary>
    /// The root node. Null if tree is empty.
    /// </summary>
    public ImmutableBallTreeNode? Root { get; }

    /// <summary>
    /// Creates an empty immutable Ball-tree.
    /// </summary>
    public ImmutableBallTree()
        : this(null)
    {
    }

    private ImmutableBallTree(ImmutableBallTreeNode? root)
    {
        Root = root;
    }

    /// <summary>
    /// Builds a new immutable Ball-tree from vectors.
    /// </summary>
    /// <param name="vectors">The vectors to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new immutable Ball-tree containing all vectors.</returns>
    public static Task<ImmutableBallTree> BuildAsync(
        VectorList vectors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);

        if (vectors.Count == 0)
        {
            return Task.FromResult(new ImmutableBallTree());
        }

        cancellationToken.ThrowIfCancellationRequested();
        var root = BuildNodes(vectors.ToList(), cancellationToken);

        return Task.FromResult(new ImmutableBallTree(root));
    }

    private static ImmutableBallTreeNode? BuildNodes(IList<Vector> vectors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (vectors.Count == 0)
        {
            return null;
        }

        if (vectors.Count == 1)
        {
            return new ImmutableBallTreeNode
            {
                Center = vectors[0],
                Radius = 0
            };
        }

        var center = Aggregate(vectors, cancellationToken);
        var radius = MaxDistance(vectors, center, cancellationToken);

        return new ImmutableBallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = BuildNodes(vectors.Take(vectors.Count / 2).ToList(), cancellationToken),
            Right = BuildNodes(vectors.Skip(vectors.Count / 2).ToList(), cancellationToken)
        };
    }

    private static Vector Aggregate(IList<Vector> vectors, CancellationToken cancellationToken)
    {
        Vector? sum = null;
        foreach (var vector in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sum is null)
            {
                sum = vector;
            }
            else
            {
                sum += vector;
            }
        }

        return sum! / vectors.Count;
    }

    private static float MaxDistance(IList<Vector> vectors, Vector center, CancellationToken cancellationToken)
    {
        var max = 0.0f;
        foreach (var vector in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var distance = vector.Distance(center);
            if (distance > max)
            {
                max = distance;
            }
        }

        return max;
    }

    /// <summary>
    /// Finds the k nearest neighbors to the query vector.
    /// This method is lock-free and can be called concurrently.
    /// </summary>
    /// <param name="query">The query vector.</param>
    /// <param name="k">The number of neighbors to find.</param>
    /// <returns>The k nearest neighbors, ordered by distance.</returns>
    public IList<Vector> Search(Vector query, int k)
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

        var result = new CappedDistanceSortedList(k);
        SearchNodes(root, query, k, result);

        return result.Select(static x => x.vector).ToList();
    }

    private static void SearchNodes(ImmutableBallTreeNode? node, Vector query, int k, CappedDistanceSortedList values)
    {
        if (node is null)
        {
            return;
        }

        var distance = query.Distance(node.Center);

        // Prune if this ball cannot contain any closer points
        if (values.Count == values.Capacity && distance - node.Radius > values.MaxDistance)
        {
            return;
        }

        // Leaf node
        if (node.Left is null && node.Right is null)
        {
            values.Add(distance, node.Center);
            return;
        }

        // Determine which child is closer
        var leftDistance = node.Left is not null ? query.Distance(node.Left.Center) : float.MaxValue;
        var rightDistance = node.Right is not null ? query.Distance(node.Right.Center) : float.MaxValue;

        var closestChild = leftDistance < rightDistance ? node.Left : node.Right;
        var furthestChild = closestChild == node.Left ? node.Right : node.Left;

        // Search closest child first, then furthest
        SearchNodes(closestChild, query, k, values);
        SearchNodes(furthestChild, query, k, values);
    }

    /// <summary>
    /// Saves the tree to a binary writer.
    /// </summary>
    public Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(CurrentFileVersion);

        // Write internal vectors (centers of internal nodes)
        var internalVectors = BuildInternalVectorsList(Root);
        WriteVectorList(writer, internalVectors, cancellationToken);

        Root?.WriteTo(writer);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads an immutable Ball-tree from a binary reader.
    /// Supports both legacy (v1) and immutable (v2) formats.
    /// </summary>
    public static async Task<ImmutableBallTree> LoadAsync(
        BinaryReader reader,
        VectorList vectors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(vectors);

        var version = reader.ReadInt32();
        if (version != 1 && version != CurrentFileVersion)
        {
            throw new InvalidDataException($"Invalid ball tree version: {version}");
        }

        // Read internal vectors
        var internalVectors = await ReadInternalVectorsAsync(reader, cancellationToken).ConfigureAwait(false);

        byte[] guidBuffer = new byte[16];
        var root = ImmutableBallTreeNode.ReadFrom(reader, vectors, internalVectors, guidBuffer);

        return new ImmutableBallTree(root);
    }

    private static Task<VectorList> ReadInternalVectorsAsync(BinaryReader reader, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var internalVectors = new VectorList();

            var fileVersion = reader.ReadInt32();
            var vectorCount = reader.ReadInt32();

            for (int i = 0; i < vectorCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nextVector = reader.ReadInt32();
                var vector = new Vector(reader.ReadBytes(nextVector));
                internalVectors.Add(vector);
            }

            return internalVectors;
        }, cancellationToken);
    }

    private static List<Vector> BuildInternalVectorsList(ImmutableBallTreeNode? node)
    {
        var vectors = new List<Vector>();
        CollectInternalVectors(node, vectors);
        return vectors;
    }

    private static void CollectInternalVectors(ImmutableBallTreeNode? node, List<Vector> internalVectors)
    {
        if (node is null)
        {
            return;
        }

        if (node.Left is not null || node.Right is not null)
        {
            internalVectors.Add(node.Center);
        }

        CollectInternalVectors(node.Left, internalVectors);
        CollectInternalVectors(node.Right, internalVectors);
    }

    private static void WriteVectorList(BinaryWriter writer, List<Vector> vectors, CancellationToken cancellationToken)
    {
        const int fileVersion = 1;
        writer.Write(fileVersion);
        writer.Write(vectors.Count);
        foreach (var v in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = v.ToBinary();
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    /// <summary>
    /// Sorted list capped at k elements, keeping the k smallest distances.
    /// </summary>
    private sealed class CappedDistanceSortedList : List<(float distance, Vector vector)>
    {
        private readonly int _k;

        public CappedDistanceSortedList(int k) : base(k + 1)
        {
            _k = k;
        }

        public float MaxDistance => Count > 0 ? this[Count - 1].distance : float.MaxValue;

        public void Add(float distance, Vector vector)
        {
            ArgumentNullException.ThrowIfNull(vector);
            Add((distance, vector));
            Sort(static (a, b) => a.distance.CompareTo(b.distance));
            if (Count > _k)
            {
                RemoveAt(Count - 1);
            }
        }
    }
}
