namespace Neighborly.Search.Utilities;

/// <summary>
/// A bounded priority queue that maintains at most k elements with the smallest priorities (distances).
/// Optimized for k-NN search where we need to track the k closest vectors efficiently.
/// </summary>
/// <typeparam name="T">The type of elements to store</typeparam>
public interface IBoundedPriorityQueue<T>
{
    /// <summary>
    /// The maximum capacity of the queue.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// The current number of elements in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Returns true if the queue contains exactly Capacity elements.
    /// </summary>
    bool IsFull { get; }

    /// <summary>
    /// The worst (largest) distance/priority currently in the queue.
    /// Returns float.MaxValue if the queue is empty.
    /// Used for pruning decisions during tree traversal.
    /// </summary>
    float WorstDistance { get; }

    /// <summary>
    /// Attempts to add an element with the given priority (distance).
    /// If the queue is full and the priority is worse than the worst element, does nothing.
    /// If the queue is full and the priority is better, removes the worst element first.
    /// </summary>
    /// <param name="element">The element to add</param>
    /// <param name="priority">The priority (distance) of the element</param>
    void TryAdd(T element, float priority);

    /// <summary>
    /// Returns all elements in the queue ordered by priority (best/smallest distance first).
    /// </summary>
    /// <returns>A list of elements sorted by priority in ascending order</returns>
    IList<T> GetResults();
}
