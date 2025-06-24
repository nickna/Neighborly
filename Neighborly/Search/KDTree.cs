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

    /// <summary>
    /// Bounded priority queue that efficiently maintains the k best candidates
    /// Uses a max-heap to keep the worst element at the top for easy removal
    /// </summary>
    private sealed class BoundedPriorityQueue
    {
        private readonly int _capacity;
        private readonly PriorityQueue<Vector, float> _heap;

        public BoundedPriorityQueue(int capacity)
        {
            _capacity = capacity;
            _heap = new PriorityQueue<Vector, float>();
        }

        public bool IsFull => _heap.Count >= _capacity;

        public float WorstDistance
        {
            get
            {
                if (_heap.Count == 0)
                    return float.MaxValue;
                
                _heap.TryPeek(out _, out var priority);
                return -priority; // Convert back from negated value
            }
        }

        public void TryAdd(Vector vector, float distance)
        {
            if (_heap.Count < _capacity)
            {
                // Use negative distance to create max-heap behavior (worst at top)
                _heap.Enqueue(vector, -distance);
            }
            else if (_heap.TryPeek(out _, out var worstPriority) && distance < -worstPriority)
            {
                _heap.Dequeue(); // Remove worst
                _heap.Enqueue(vector, -distance);
            }
        }

        public IList<Vector> GetResults()
        {
            var results = new List<Vector>(_heap.Count);
            var tempQueue = new PriorityQueue<Vector, float>();
            
            // Extract all elements and re-negate to get correct distances for sorting
            while (_heap.TryDequeue(out var vector, out var priority))
            {
                tempQueue.Enqueue(vector, priority); // Keep negative for min-heap ordering
            }
            
            // Dequeue in ascending distance order (best first)
            while (tempQueue.TryDequeue(out var vector, out _))
            {
                results.Add(vector);
            }
            
            return results;
        }
    }

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

        var candidates = new BoundedPriorityQueue(k);
        NearestNeighbors(root, query, k, 0, candidates);
        
        return candidates.GetResults();
    }

    private void NearestNeighbors(KDTreeNode? node, Vector query, int k, int depth, BoundedPriorityQueue candidates)
    {
        if (node?.Vector == null)
            return;

        var axis = depth % query.Dimensions;
        var distance = (node.Vector - query).Magnitude;
        
        // Add current node to candidates
        candidates.TryAdd(node.Vector, distance);

        // Determine which child to search first
        var nearChild = query[axis] <= node.Vector[axis] ? node.Left : node.Right;
        var farChild = query[axis] <= node.Vector[axis] ? node.Right : node.Left;

        // Search the near child first
        NearestNeighbors(nearChild, query, k, depth + 1, candidates);

        // Check if we need to search the far child (pruning condition)
        var axisDistance = Math.Abs(query[axis] - node.Vector[axis]);
        if (!candidates.IsFull || axisDistance < candidates.WorstDistance)
        {
            NearestNeighbors(farChild, query, k, depth + 1, candidates);
        }
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
