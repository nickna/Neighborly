using Neighborly.Search.Utilities;

namespace Neighborly.Search;

public class BallTree : ISerializableSearchIndex
{

    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

    private BallTreeNode? root;

    /// <summary>
    /// Returns true if the index has been built and is ready for searches.
    /// </summary>
    public bool IsBuilt => root != null;

    /// <summary>
    /// The file format version this implementation writes.
    /// </summary>
    public int FileFormatVersion => s_currentFileVersion;

    /// <summary>
    /// Builds the BallTree index asynchronously with cancellation support.
    /// </summary>
    /// <param name="vectors">The vectors to index.</param>
    /// <param name="cancellationToken">Cancellation token to stop the build operation.</param>
    public Task BuildAsync(VectorList vectors, CancellationToken cancellationToken = default)
    {
        if (vectors.Count == 0)
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();
        root = BuildNodes(vectors, cancellationToken);
        return Task.CompletedTask;
    }

    private static BallTreeNode? BuildNodes(Span<Vector> vectors)
    {
        if (vectors.IsEmpty)
            return null;

        if (vectors.Length == 1)
            return new BallTreeNode
            {
                Center = vectors[0],
                Radius = 0
            };

        var center = TreeBuildingUtilities.AggregateSum(vectors) / vectors.Length;
        var radius = TreeBuildingUtilities.CalculateMaxDistance(vectors, center);

        return new BallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = BuildNodes(vectors[..(vectors.Length / 2)]),
            Right = BuildNodes(vectors[(vectors.Length / 2)..])
        };
    }

    public async Task LoadAsync(BinaryReader reader, VectorList vectors, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(vectors);

        var version = reader.ReadInt32(); // Read the version number
        if (version != s_currentFileVersion)
        {
            throw new InvalidDataException($"Invalid ball tree version: {version}");
        }

        root = null;

        // Read internal vectors (centers of internal nodes) directly without creating nested VectorDatabase
        var internalVectors = await ReadInternalVectorsAsync(reader, cancellationToken).ConfigureAwait(false);

        byte[] guidBuffer = new byte[16];
        // Read the tree starting at the root node
        root = BallTreeNode.ReadFrom(reader, vectors, internalVectors, guidBuffer);
    }

    private static Task<VectorList> ReadInternalVectorsAsync(BinaryReader reader, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var internalVectors = new VectorList();
            
            // Read the file version (skip it as it's already been read by the calling method in a VectorDatabase context)
            var fileVersion = reader.ReadInt32();
            
            // Read vector count
            var vectorCount = reader.ReadInt32();
            
            // Read vectors directly into VectorList without creating VectorDatabase
            for (int i = 0; i < vectorCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nextVector = reader.ReadInt32();    // File offset of the next Vector
                var vector = new Vector(reader.ReadBytes(nextVector));
                internalVectors.Add(vector);
            }
            
            return internalVectors;
        }, cancellationToken);
    }

    public Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(s_currentFileVersion); // Write the version number

        // Write internal vectors (centers of internal nodes) directly without creating VectorDatabase
        var internalVectors = BuildInternalVectorsList(root);
        WriteVectorList(writer, internalVectors, cancellationToken);

        root?.WriteTo(writer);
        return Task.CompletedTask;
    }

    private static void WriteVectorList(BinaryWriter writer, List<Vector> vectors, CancellationToken cancellationToken)
    {
        // Write in same format as VectorDatabase.WriteToAsync
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

    private static List<Vector> BuildInternalVectorsList(BallTreeNode? node)
    {
        var vectors = new List<Vector>();
        CollectInternalVectors(node, vectors);
        return vectors;
    }

    private static void CollectInternalVectors(BallTreeNode? node, List<Vector> internalVectors)
    {
        if (node == null)
            return;

        if (node.Left != null || node.Right != null)
        {
            internalVectors.Add(node.Center);
        }

        CollectInternalVectors(node.Left, internalVectors);
        CollectInternalVectors(node.Right, internalVectors);
    }

    private BallTreeNode? BuildNodes(IList<Vector> vectors)
    {
        return BuildNodes(vectors, CancellationToken.None);
    }

    private BallTreeNode? BuildNodes(IList<Vector> vectors, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (vectors.Count <= 0)
        {
            return null;
        }

        if (vectors.Count == 1)
        {
            return new BallTreeNode
            {
                Center = vectors[0],
                Radius = 0
            };
        }

        var center = TreeBuildingUtilities.CalculateCentroid(vectors, cancellationToken);
        var radius = TreeBuildingUtilities.CalculateMaxDistance(vectors, center, cancellationToken);

        return new BallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = BuildNodes(vectors.Take(vectors.Count / 2).ToList(), cancellationToken),
            Right = BuildNodes(vectors.Skip(vectors.Count / 2).ToList(), cancellationToken)
        };
    }


    public IList<Vector> Search(Vector query, int k)
    {
        var result = new BoundedPriorityQueue<Vector>(k);
        Search(root, query, k, result);
        return result.GetResults();
    }


    private static void Search(BallTreeNode? node, Vector query, int k, IBoundedPriorityQueue<Vector> values)
    {
        if (node == null)
            return;

        var distance = query.Distance(node.Center);
        if (values.IsFull && distance - node.Radius > values.WorstDistance)
            return;

        if (node.Left == null && node.Right == null)
        {
            values.TryAdd(node.Center, distance);
            return;
        }

        var closestChild = query.Distance(node.Left!.Center) < query.Distance(node.Right!.Center) ? node.Left : node.Right;
        var furthestChild = closestChild == node.Left ? node.Right : node.Left;
        Search(closestChild, query, k, values);
        Search(furthestChild, query, k, values);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BallTree other)
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

