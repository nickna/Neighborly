namespace Neighborly.Search.Utilities;

/// <summary>
/// Heap-based bounded priority queue for single-threaded k-NN search.
/// Uses a max-heap (via negated priorities) to efficiently maintain the k best candidates.
/// Time complexity: O(log k) for TryAdd, O(k log k) for GetResults.
/// This implementation is NOT thread-safe. Use ThreadSafeBoundedPriorityQueue for concurrent scenarios.
/// </summary>
/// <typeparam name="T">The type of elements to store</typeparam>
public sealed class BoundedPriorityQueue<T> : IBoundedPriorityQueue<T>
{
    private readonly int _capacity;
    private readonly PriorityQueue<T, float> _heap;

    public BoundedPriorityQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");

        _capacity = capacity;
        _heap = new PriorityQueue<T, float>(capacity);
    }

    public int Capacity => _capacity;
    public int Count => _heap.Count;
    public bool IsFull => _heap.Count >= _capacity;

    public float WorstDistance
    {
        get
        {
            if (_heap.Count == 0)
                return float.MaxValue;

            // Peek at the top (worst element due to max-heap behavior)
            _heap.TryPeek(out _, out var priority);
            return -priority; // Convert back from negated value
        }
    }

    public void TryAdd(T element, float priority)
    {
        if (_heap.Count < _capacity)
        {
            // Use negative priority to create max-heap behavior (worst at top)
            // PriorityQueue is a min-heap, so we negate to get max-heap
            _heap.Enqueue(element, -priority);
        }
        else if (_heap.TryPeek(out _, out var worstPriority) && priority < -worstPriority)
        {
            // New element is better than worst, remove worst and add new
            _heap.Dequeue();
            _heap.Enqueue(element, -priority);
        }
    }

    public IList<T> GetResults()
    {
        if (_heap.Count == 0)
            return [];

        var results = new List<T>(_heap.Count);
        var tempQueue = new PriorityQueue<T, float>(_heap.Count);

        // Extract all elements (still negated) and put in temp queue
        while (_heap.TryDequeue(out var element, out var priority))
        {
            tempQueue.Enqueue(element, priority);
        }

        // Dequeue from temp in ascending distance order (best first)
        // The negated priorities naturally sort smallest distances first
        while (tempQueue.TryDequeue(out var element, out _))
        {
            results.Add(element);
        }

        return results;
    }
}
