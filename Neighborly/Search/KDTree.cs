using System;
using System.Collections.Generic;
using System.Linq;

namespace Neighborly.Search;

/// <summary>
/// K-D Tree search (see Wikipedia: https://en.wikipedia.org/wiki/K-d_tree)
/// </summary>
public class KDTree
{
    private KDTreeNode? root;

    public void Build(VectorList vectors)
    {
        if (vectors == null)
        {
            throw new ArgumentNullException(nameof(vectors), "Vector list cannot be null");
        }
        if (vectors.Count == 0)
        {
            return;
        }

        root = Build(vectors.ToArray(), 0, false);
    }

    private KDTreeNode? Build(Span<Vector> vectors, int depth, bool isSorted)
    {
        if (vectors.IsEmpty || vectors[0].Dimensions == 0)
            return null;

        var axis = depth % vectors[0].Dimensions;

        if (!isSorted)
        {
            vectors.Sort((a, b) => a[axis].CompareTo(b[axis]));
        }

        var median = vectors.Length / 2;

        return new KDTreeNode
        {
            Vector = vectors[median],
            Left = Build(vectors[..median], depth + 1, true),
            Right = Build(vectors[(median + 1)..], depth + 1, true)
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

        return NearestNeighbors(root, query, k, 0)?
            .OrderBy(t => (t.Item1 - query).Magnitude)
            .Select(t => t.Item1)
            .ToList() ?? new List<Vector>();

    }

    private List<Tuple<Vector, double>>? NearestNeighbors(KDTreeNode? node, Vector query, int k, int depth)
    {
        if (node == null || node.Vector == null)
            return new List<Tuple<Vector, double>>();

        var axis = depth % query.Dimensions;
        var next = node.Vector[axis] > query[axis] ? node.Left : node.Right;
        var others = node.Vector[axis] > query[axis] ? node.Right : node.Left;

        var best = NearestNeighbors(next, query, k, depth + 1) ?? new List<Tuple<Vector, double>>();
        if (best.Count < k || Math.Abs(node.Vector[axis] - query[axis]) < best.Last().Item2)
        {
            var distance = (node.Vector - query).Magnitude;
            best.Add(Tuple.Create(node.Vector, (double)distance));
            best = best.OrderBy(t => t.Item2).Take(k).ToList();

            if (best.Count < k || Math.Abs(node.Vector[axis] - query[axis]) < best.Last().Item2)
            {
                best = best.Concat(NearestNeighbors(others, query, k, depth + 1) ?? new List<Tuple<Vector, double>>())
                    .OrderBy(t => t.Item2)
                    .Take(k)
                    .ToList();
            }
        }

        return best;
    }

    public IList<Vector> Search(Vector query, int k)
    {
        // Perform the nearest neighbor search
        var results = NearestNeighbors(query, k);

        return results;
    }

}
