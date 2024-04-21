using System;
using System.Collections.Generic;
using System.Linq;

namespace Neighborly
{
    public class KDTree
    {
        private Node? root;

        public void Build(IList<Vector> vectors)
        {
            if (vectors == null)
            {
                throw new ArgumentNullException(nameof(vectors), "Vector list cannot be null");
            }

            root = Build(vectors, 0);
        }

        private Node? Build(IList<Vector> vectors, int depth)
        {
            if (!vectors.Any())
                return null;

            var axis = depth % vectors[0].Dimensions;
            var sortedVectors = vectors.OrderBy(v => v[axis]).ToList();

            var median = sortedVectors.Count / 2;

            return new Node
            {
                Vector = sortedVectors[median],
                Left = Build(sortedVectors.Take(median).ToList(), depth + 1),
                Right = Build(sortedVectors.Skip(median + 1).ToList(), depth + 1)
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

        private List<Tuple<Vector, double>>? NearestNeighbors(Node? node, Vector query, int k, int depth)
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

        private class Node
        {
            public Vector? Vector { get; set; }
            public Node? Left { get; set; }
            public Node? Right { get; set; }
        }
    }
}
