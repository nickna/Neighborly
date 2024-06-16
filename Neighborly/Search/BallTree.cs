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

    public void Load(BinaryReader reader, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(vectors);

        var version = reader.ReadInt32(); // Read the version number
        if (version != s_currentFileVersion)
        {
            throw new InvalidDataException($"Invalid ball tree version: {version}");
        }

        root = null;
        var entries = reader.ReadInt32();
        // Layout of the each entry in the file:
        // - Center (Guid)
        // - Radius (double)
        // - Left (Guid of the Vector in the left node)
        // - Right (Guid of the Vector in the left node)
        Span<byte> guidBuffer = stackalloc byte[16];
        List<(Vector center, double radius, Vector? left, Vector? right)> nodes = new(entries);
        for (var i = 0; i < entries; i++)
        {
            // Read the entry
            var center = reader.ReadGuid(guidBuffer);
            var radius = reader.ReadDouble();
            var left = reader.ReadGuid(guidBuffer);
            var right = reader.ReadGuid(guidBuffer);

            // Find the vectors
            var centerVector = vectors.GetById(center);
            if (centerVector is null)
            {
                throw new InvalidDataException($"Vector not found: {center}");
            }

            var leftVector = vectors.GetById(left);
            var rightVector = vectors.GetById(right);
            nodes.Add((centerVector, radius, leftVector, rightVector));
        }
    }

    public void Save(BinaryWriter writer, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(vectors);

        writer.Write(s_currentFileVersion); // Write the version number
        var entries = root?.Count() ?? 0;
        writer.Write(entries);

        root?.WriteTo(writer);
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


}
