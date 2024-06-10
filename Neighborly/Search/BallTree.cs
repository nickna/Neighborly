namespace Neighborly.Search;
public class BallTree
{
    private BallTreeNode root;

    public void Build(VectorList vectors)
    {
        if (vectors.Count == 0)
            return;
        
        root = BuildNodes(vectors);
    }

    private BallTreeNode BuildNodes(IList<Vector> vectors)
    {
        if (!vectors.Any())
            return null;

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

    private IList<Vector> Search(BallTreeNode node, Vector query, int k)
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
