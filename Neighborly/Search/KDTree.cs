using System;
using System.Collections.Generic;
using System.Linq;

namespace Neighborly.Search;

/// <summary>
/// K-D Tree search (see Wikipedia: https://en.wikipedia.org/wiki/K-d_tree)
/// </summary>
public class KDTree
{
    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

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

        root = Build(vectors, 0);
    }

    public void Load(BinaryReader reader, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(vectors);

        var version = reader.ReadInt32(); // Read the version number
        if (version != s_currentFileVersion)
        {
            throw new InvalidDataException($"Invalid KD tree version: {version}");
        }

        root = null;
        Span<byte> guidBuffer = stackalloc byte[16];
        // Read the tree starting at the root node
        root = KDTreeNode.ReadFrom(reader, vectors, guidBuffer);
    }

    public void Save(BinaryWriter writer, VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(vectors);

        writer.Write(s_currentFileVersion); // Write the version number

        root?.WriteTo(writer);
    }

    private KDTreeNode? Build(IList<Vector> vectors, int depth)
    {
        if (vectors.Count <= 0)
        {
            return null;
        }

        var firstVector = vectors[0];
        if (firstVector.Dimensions == 0)
        {
            return null;
        }

        var axis = depth % firstVector.Dimensions;
        var sortedVectors = vectors.OrderBy(v => v[axis]).ToList();

        var median = sortedVectors.Count / 2;

        return new KDTreeNode
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

    public override bool Equals(object? obj)
    {
        if (obj is not KDTree other)
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
