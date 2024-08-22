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

        var center = Aggregate(vectors) / vectors.Length;
        var radius = MaxDistance(vectors, center);

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

        // Read internal vectors (centers of internal nodes)
        using var internalVectors = new VectorDatabase();
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
        using var internalVectors = BuildInternalVectors(root);
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

    private static float MaxDistance(Span<Vector> vectors, Vector center)
    {
        var max = 0.0f;
        foreach (var vector in vectors)
        {
            var distance = vector.Distance(center);
            if (distance > max)
            {
                max = distance;
            }
        }

        return max;
    }

    private static Vector Aggregate(Span<Vector> vectors)
    {
        Vector? sum = null;
        foreach (var vector in vectors)
        {
            if (sum == null)
            {
                sum = vector;
            }
            else
            {
                sum += vector;
            }
        }

        return sum!;
    }

    public IList<Vector> Search(Vector query, int k)
    {
        var result = new CappedDistanceSortedList(k);
        Search(root, query, k, result);
        return result.Select(static x => x.vector).ToList();
    }


    private static void Search(BallTreeNode? node, Vector query, int k, CappedDistanceSortedList values)
    {
        if (node == null)
            return;

        var distance = query.Distance(node.Center);
        if (values.Count == values.Capacity && distance - node.Radius > values.MaxDistance)
            return;

        if (node.Left == null && node.Right == null)
        {
            values.Add(distance, node.Center);
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

    private sealed class CappedDistanceSortedList(int k) : List<(float distance, Vector vector)>(k + 1)
    {
        private readonly int _k = k;

        public float MaxDistance => Count > 0 ? this[0].distance : float.MaxValue;

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

    public override int GetHashCode()
    {
        return root?.GetHashCode() ?? 0;
    }
}

