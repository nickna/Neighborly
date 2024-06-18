namespace Neighborly.Search;

public class BallTree
{
    private BallTreeNode? root;

    public void Build(VectorList vectors)
    {
        if (vectors.Count == 0)
            return;

        root = BuildNodes(vectors.ToArray());
    }

    private BallTreeNode? BuildNodes(Span<Vector> vectors)
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
        return Search(root, query, k);
    }

    private IList<Vector> Search(BallTreeNode? node, Vector query, int k)
    {
        if (node == null)
            return new List<Vector>();

        var distance = query.Distance(node.Center);
        if (distance > node.Radius + k)
            return new List<Vector>();

        return Search(node.Left, query, k)
            .Concat(Search(node.Right, query, k))
            .OrderBy(v => v.Distance(query))
            .Take(k)
            .ToList();
    }


}
