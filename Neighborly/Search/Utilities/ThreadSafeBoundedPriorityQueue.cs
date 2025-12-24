using System.Collections.Concurrent;

namespace Neighborly.Search.Utilities;

/// <summary>
/// Thread-safe bounded priority queue for parallel k-NN search operations.
/// Uses a concurrent queue with periodic sorting under lock for thread safety.
/// Time complexity: O(1) for TryAdd (amortized with periodic O(k log k) sorts), thread-safe.
/// This implementation is optimized for scenarios with high contention from multiple threads.
/// </summary>
/// <typeparam name="T">The type of elements to store</typeparam>
public sealed class ThreadSafeBoundedPriorityQueue<T> : IBoundedPriorityQueue<T>
{
    private readonly int _capacity;
    private readonly ConcurrentQueue<(T element, float priority)> _candidates;
    private readonly object _lock = new();
    private volatile int _count = 0;
    private const int TrimThreshold = 2; // Trim when we exceed capacity * this factor

    public ThreadSafeBoundedPriorityQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0");

        _capacity = capacity;
        _candidates = new ConcurrentQueue<(T, float)>();
    }

    public int Capacity => _capacity;
    public int Count => _count;
    public bool IsFull => _count >= _capacity;

    public float WorstDistance
    {
        get
        {
            lock (_lock)
            {
                if (_count == 0)
                    return float.MaxValue;

                var items = _candidates.ToArray();
                if (items.Length == 0)
                    return float.MaxValue;

                return items.Max(item => item.priority);
            }
        }
    }

    public void TryAdd(T element, float priority)
    {
        // Always add to concurrent queue
        _candidates.Enqueue((element, priority));
        Interlocked.Increment(ref _count);

        // Periodically trim to avoid unbounded growth
        // Only lock and trim when we significantly exceed capacity
        if (_count > _capacity * TrimThreshold)
        {
            TrimToCapacity();
        }
    }

    private void TrimToCapacity()
    {
        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_count <= _capacity * TrimThreshold)
                return;

            var items = new List<(T element, float priority)>(_candidates.Count);

            // Drain the queue
            while (_candidates.TryDequeue(out var item))
            {
                items.Add(item);
            }

            // Sort by priority (ascending) and keep only the best k
            items.Sort((a, b) => a.priority.CompareTo(b.priority));
            var bestItems = items.Take(_capacity).ToList();

            // Re-enqueue the best items
            foreach (var item in bestItems)
            {
                _candidates.Enqueue(item);
            }

            _count = bestItems.Count;
        }
    }

    public IList<T> GetResults()
    {
        lock (_lock)
        {
            var items = _candidates.ToArray();

            if (items.Length == 0)
                return [];

            // Sort by priority (ascending) and take up to capacity
            Array.Sort(items, (a, b) => a.priority.CompareTo(b.priority));

            return items
                .Take(_capacity)
                .Select(item => item.element)
                .ToList();
        }
    }
}
