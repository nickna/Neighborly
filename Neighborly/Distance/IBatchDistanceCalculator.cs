namespace Neighborly.Distance;

/// <summary>
/// Extends IDistanceCalculator with batch processing capabilities for calculating distances
/// between one query vector and multiple candidate vectors efficiently.
/// </summary>
public interface IBatchDistanceCalculator : IDistanceCalculator
{
    /// <summary>
    /// Calculates distances between a query vector and multiple candidate vectors in a batch.
    /// </summary>
    /// <param name="query">The query vector to compare against all candidates.</param>
    /// <param name="candidates">The collection of candidate vectors.</param>
    /// <param name="results">Pre-allocated span to store the calculated distances. Must have length >= candidates.Count.</param>
    /// <exception cref="ArgumentNullException">If query or candidates is null.</exception>
    /// <exception cref="ArgumentException">If results span is too small or if vector dimensions don't match.</exception>
    void CalculateDistances(Vector query, IList<Vector> candidates, Span<float> results);

    /// <summary>
    /// Calculates distances between a query vector and multiple candidate vectors, returning the results as an array.
    /// </summary>
    /// <param name="query">The query vector to compare against all candidates.</param>
    /// <param name="candidates">The collection of candidate vectors.</param>
    /// <returns>Array of distances corresponding to each candidate vector.</returns>
    /// <exception cref="ArgumentNullException">If query or candidates is null.</exception>
    /// <exception cref="ArgumentException">If vector dimensions don't match.</exception>
    float[] CalculateDistances(Vector query, IList<Vector> candidates);

    /// <summary>
    /// Calculates distances for candidates within the specified index range.
    /// Useful for parallel processing of large candidate sets.
    /// </summary>
    /// <param name="query">The query vector to compare against candidates.</param>
    /// <param name="candidates">The collection of candidate vectors.</param>
    /// <param name="startIndex">The starting index in the candidates collection.</param>
    /// <param name="count">The number of candidates to process.</param>
    /// <param name="results">Pre-allocated span to store the calculated distances.</param>
    /// <exception cref="ArgumentNullException">If query or candidates is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If index range is invalid.</exception>
    void CalculateDistancesRange(Vector query, IList<Vector> candidates, int startIndex, int count, Span<float> results);

    /// <summary>
    /// Gets a value indicating whether this calculator supports optimized batch operations.
    /// If false, the default implementation will fall back to sequential calculations.
    /// </summary>
    bool SupportsBatchOptimization { get; }

    /// <summary>
    /// Gets the optimal batch size for this calculator based on the vector dimension.
    /// This helps with memory allocation and cache efficiency.
    /// </summary>
    /// <param name="dimension">The dimension of the vectors being processed.</param>
    /// <returns>The recommended batch size for optimal performance.</returns>
    int GetOptimalBatchSize(int dimension);
}