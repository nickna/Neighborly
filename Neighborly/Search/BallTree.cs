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
}