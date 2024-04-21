namespace Neighborly;
public class BallTree
{
    private Node root;

    public BallTree(IList<Vector> vectors)
    {
        root = Build(vectors);
    }

    private Node Build(IList<Vector> vectors)
    {
        if (!vectors.Any())
            return null;

        var center = vectors.Aggregate((a, b) => a + b) / vectors.Count;
        var radius = vectors.Max(v => v.Distance(center));

        return new Node
        {
            Center = center,
            Radius = radius,
            Left = Build(vectors.Take(vectors.Count / 2).ToList()),
            Right = Build(vectors.Skip(vectors.Count / 2).ToList())
        };
    }

    public IList<Vector> Search(Vector query, int k)
    {
        return Search(root, query, k);
    }

    private IList<Vector> Search(Node node, Vector query, int k)
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

    private class Node
    {
        public Vector Center { get; set; }
        public double Radius { get; set; }
        public Node Left { get; set; }
        public Node Right { get; set; }
    }
}
