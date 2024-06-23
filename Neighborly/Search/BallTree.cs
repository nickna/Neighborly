namespace Neighborly.Search;

public class BallTree
{
    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

    private BallTreeNode? root;

    public void Build(VectorList vectors)
    {
        if (vectors.Count == 0)
            return;

        root = BuildNodes(vectors);
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

        // Read internal vectors (centers of internal nodes)
        var internalVectors = new VectorDatabase();
        await internalVectors.ReadFromAsync(reader, false, cancellationToken).ConfigureAwait(false);

        byte[] guidBuffer = new byte[16];
        // Read the tree starting at the root node
        root = BallTreeNode.ReadFrom(reader, vectors, internalVectors, guidBuffer);
    }

    public async Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(s_currentFileVersion); // Write the version number

        // Write internal vectors (centers of internal nodes)
        var internalVectors = BuildInternalVectors(root);
        await internalVectors.WriteToAsync(writer, false, cancellationToken).ConfigureAwait(false);

        root?.WriteTo(writer);
    }

    private static VectorDatabase BuildInternalVectors(BallTreeNode? node)
    {
        return BuildInternalVectors(node, new VectorDatabase());
    }

    private static VectorDatabase BuildInternalVectors(BallTreeNode? node, VectorDatabase internalVectors)
    {
        if (node == null)
            return internalVectors;

        if (node.Left != null || node.Right != null)
        {
            internalVectors.Vectors.Add(node.Center);
        }

        internalVectors = BuildInternalVectors(node.Left, internalVectors);
        internalVectors = BuildInternalVectors(node.Right, internalVectors);

        return internalVectors;
    }

    private BallTreeNode? BuildNodes(IList<Vector> vectors)
    {
        if (vectors.Count <= 0)
        {
            return null;
        }

        if (vectors.Count == 1)
            return new BallTreeNode
            {
                Center = vectors[0],
                Radius = 0
            };

        var center = vectors.Aggregate((a, b) => a + b) / vectors.Count;
        var radius = vectors.Max(v => v.Distance(center));

        return new BallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = BuildNodes(vectors.Take(vectors.Count / 2).ToList()),
            Right = BuildNodes(vectors.Skip(vectors.Count / 2).ToList())
        };
    }

    public IList<Vector> Search(Vector query, int k)
    {
        return Search(root, query, k);
    }

    private IList<Vector> Search(BallTreeNode? node, Vector query, int k)
    {
        if (node == null)
            return [];

        var distance = query.Distance(node.Center);
        if (distance > node.Radius + k)
            return [];

        return Search(node.Left, query, k)
            .Concat(Search(node.Right, query, k))
            .OrderBy(v => v.Distance(query))
            .Take(k)
            .ToList();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BallTree other)
        {
            return false;
        }

        return Equals(root, other.root);
    }


}
