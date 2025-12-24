namespace Neighborly.Search.Utilities;

/// <summary>
/// Utility methods for building tree-based search indexes (KD-Tree, Ball-Tree).
/// Consolidates shared logic across mutable and immutable tree implementations.
/// </summary>
internal static class TreeBuildingUtilities
{
    /// <summary>
    /// Calculates the centroid (mean) of a collection of vectors.
    /// </summary>
    /// <param name="vectors">The vectors to aggregate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The centroid vector (sum / count).</returns>
    public static Vector CalculateCentroid(IList<Vector> vectors, CancellationToken cancellationToken = default)
    {
        if (vectors.Count == 0)
        {
            throw new ArgumentException("Cannot calculate centroid of empty vector collection", nameof(vectors));
        }

        Vector? sum = null;
        foreach (var vector in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sum is null)
            {
                sum = vector;
            }
            else
            {
                sum += vector;
            }
        }

        return sum! / vectors.Count;
    }

    /// <summary>
    /// Calculates the maximum distance from any vector to a center point.
    /// </summary>
    /// <param name="vectors">The vectors to measure.</param>
    /// <param name="center">The center point.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The maximum distance.</returns>
    public static float CalculateMaxDistance(IList<Vector> vectors, Vector center, CancellationToken cancellationToken = default)
    {
        var max = 0.0f;
        foreach (var vector in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var distance = vector.Distance(center);
            if (distance > max)
            {
                max = distance;
            }
        }

        return max;
    }

    /// <summary>
    /// Span-based variant for calculating maximum distance (no cancellation).
    /// </summary>
    public static float CalculateMaxDistance(Span<Vector> vectors, Vector center)
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

    /// <summary>
    /// Aggregates vectors by summing them (used for Span-based Ball Tree construction).
    /// </summary>
    /// <param name="vectors">The vectors to aggregate.</param>
    /// <returns>The sum of all vectors.</returns>
    public static Vector AggregateSum(Span<Vector> vectors)
    {
        if (vectors.IsEmpty)
        {
            throw new ArgumentException("Cannot aggregate empty vector collection", nameof(vectors));
        }

        Vector? sum = null;
        foreach (var vector in vectors)
        {
            if (sum is null)
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
}
