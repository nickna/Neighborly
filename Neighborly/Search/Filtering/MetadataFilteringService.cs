using Neighborly.Distance;

namespace Neighborly.Search.Filtering;

/// <summary>
/// Service responsible for performing searches with metadata filtering.
/// Implements optimized filtering strategies for different search algorithms.
/// </summary>
internal sealed class MetadataFilteringService
{
    private readonly VectorList _vectors;
    private readonly Func<Vector, int, SearchAlgorithm, float, IList<Vector>> _searchDelegate;

    /// <summary>
    /// Initializes a new instance of the MetadataFilteringService.
    /// </summary>
    /// <param name="vectors">The vector collection to search.</param>
    /// <param name="searchDelegate">Delegate to perform unfiltered search operations.</param>
    public MetadataFilteringService(VectorList vectors, Func<Vector, int, SearchAlgorithm, float, IList<Vector>> searchDelegate)
    {
        _vectors = vectors ?? throw new ArgumentNullException(nameof(vectors));
        _searchDelegate = searchDelegate ?? throw new ArgumentNullException(nameof(searchDelegate));
    }

    /// <summary>
    /// Performs search with metadata filtering using the optimal strategy for the given algorithm.
    /// </summary>
    public IList<Vector> SearchWithFilter(Vector query, int k, SearchAlgorithm method, float similarityThreshold, Func<Vector, bool> filterPredicate)
    {
        return method switch
        {
            SearchAlgorithm.Linear => LinearSearchWithFilter(query, k, filterPredicate),
            SearchAlgorithm.KDTree or SearchAlgorithm.BallTree => TreeSearchWithFilter(query, k, method, similarityThreshold, filterPredicate),
            SearchAlgorithm.LSH or SearchAlgorithm.HNSW or SearchAlgorithm.BinaryQuantization or SearchAlgorithm.ProductQuantization
                => ApproximateSearchWithFilter(query, k, method, similarityThreshold, filterPredicate),
            _ => throw new NotSupportedException($"Metadata filtering is not supported for algorithm: {method}")
        };
    }

    /// <summary>
    /// Performs range search with metadata filtering.
    /// </summary>
    public IList<Vector> RangeSearchWithFilter(Vector query, float radius, IDistanceCalculator distanceCalculator, Func<Vector, bool> filterPredicate)
    {
        var candidates = new List<Vector>();

        // Apply filter during candidate collection
        foreach (var vector in _vectors)
        {
            if (!filterPredicate(vector))
                continue;

            var distance = distanceCalculator.CalculateDistance(query, vector);
            if (distance <= radius)
            {
                candidates.Add(vector);
            }
        }

        // Sort by distance
        return candidates.OrderBy(v => distanceCalculator.CalculateDistance(query, v)).ToList();
    }

    /// <summary>
    /// Linear search with integrated metadata filtering.
    /// </summary>
    private IList<Vector> LinearSearchWithFilter(Vector query, int k, Func<Vector, bool> filterPredicate)
    {
        var candidates = new List<(Vector vector, float distance)>();

        foreach (var vector in _vectors)
        {
            if (!filterPredicate(vector))
                continue;

            var distance = vector.Distance(query);
            candidates.Add((vector, distance));
        }

        return candidates
            .OrderBy(c => c.distance)
            .Take(k)
            .Select(c => c.vector)
            .ToList();
    }

    /// <summary>
    /// Tree-based search with metadata filtering using dynamic expansion and selectivity optimization.
    /// For tree algorithms, we may need to search beyond k to find enough filtered results.
    /// </summary>
    private IList<Vector> TreeSearchWithFilter(Vector query, int k, SearchAlgorithm method, float similarityThreshold, Func<Vector, bool> filterPredicate)
    {
        // Estimate filter selectivity to optimize search strategy
        var selectivity = EstimateFilterSelectivity(filterPredicate, Math.Min(1000, _vectors.Count));

        // Dynamic expansion based on estimated selectivity
        int expandedK = CalculateOptimalExpansion(k, selectivity, _vectors.Count);

        // Special handling for very low selectivity - use linear search directly
        if (selectivity < 0.01 && _vectors.Count > 5000)
        {
            return LinearSearchWithFilter(query, k, filterPredicate);
        }

        var candidates = _searchDelegate(query, expandedK, method, similarityThreshold);
        var filteredResults = candidates.Where(filterPredicate).Take(k).ToList();

        // Progressive expansion if we need more results
        int maxAttempts = 3;
        int attempt = 1;

        while (filteredResults.Count < k && expandedK < _vectors.Count && attempt < maxAttempts)
        {
            // Increase expansion factor progressively
            int newExpandedK = Math.Min(_vectors.Count, expandedK * 2);
            if (newExpandedK == expandedK) break; // No more expansion possible

            candidates = _searchDelegate(query, newExpandedK, method, similarityThreshold);
            filteredResults = candidates.Where(filterPredicate).Take(k).ToList();

            expandedK = newExpandedK;
            attempt++;
        }

        // Final fallback to linear search if still insufficient results
        if (filteredResults.Count < k / 2) // Only if we have very few results
        {
            return LinearSearchWithFilter(query, k, filterPredicate);
        }

        return filteredResults;
    }

    /// <summary>
    /// Approximate search algorithms with metadata filtering using dynamic expansion.
    /// </summary>
    private IList<Vector> ApproximateSearchWithFilter(Vector query, int k, SearchAlgorithm method, float similarityThreshold, Func<Vector, bool> filterPredicate)
    {
        // Estimate filter selectivity for approximate algorithms
        var selectivity = EstimateFilterSelectivity(filterPredicate, Math.Min(500, _vectors.Count));

        // Conservative expansion for approximate algorithms (they're already approximate)
        int baseExpansion = (int)(CalculateOptimalExpansion(k, selectivity, _vectors.Count) * 0.7);
        int expandedK = Math.Max(k * 2, Math.Min(baseExpansion, _vectors.Count));

        var candidates = _searchDelegate(query, expandedK, method, similarityThreshold);
        var filteredResults = candidates.Where(filterPredicate).Take(k).ToList();

        // For approximate algorithms, be more aggressive about fallback due to quality trade-offs
        if (filteredResults.Count < k * 0.6) // If we have less than 60% of what we need
        {
            // Try one more expansion before falling back to linear
            if (expandedK < _vectors.Count)
            {
                int secondExpansion = Math.Min(_vectors.Count, expandedK * 2);
                candidates = _searchDelegate(query, secondExpansion, method, similarityThreshold);
                filteredResults = candidates.Where(filterPredicate).Take(k).ToList();
            }

            // Final fallback to linear search if still insufficient
            if (filteredResults.Count < k * 0.4)
            {
                return LinearSearchWithFilter(query, k, filterPredicate);
            }
        }

        return filteredResults;
    }

    /// <summary>
    /// Estimates the selectivity of a metadata filter by sampling a subset of vectors.
    /// </summary>
    /// <param name="filterPredicate">The filter predicate to test</param>
    /// <param name="sampleSize">Number of vectors to sample for estimation</param>
    /// <returns>Estimated selectivity as a ratio between 0 and 1</returns>
    private double EstimateFilterSelectivity(Func<Vector, bool> filterPredicate, int sampleSize)
    {
        if (_vectors.Count == 0) return 0.0;

        sampleSize = Math.Min(sampleSize, _vectors.Count);
        int matches = 0;

        // Use systematic sampling for better representation
        int step = Math.Max(1, _vectors.Count / sampleSize);
        int sampledCount = 0;

        for (int i = 0; i < _vectors.Count && sampledCount < sampleSize; i += step)
        {
            if (filterPredicate(_vectors[i]))
            {
                matches++;
            }
            sampledCount++;
        }

        return sampledCount > 0 ? (double)matches / sampledCount : 0.0;
    }

    /// <summary>
    /// Calculates the optimal expansion factor based on filter selectivity.
    /// </summary>
    /// <param name="k">Requested number of results</param>
    /// <param name="selectivity">Estimated filter selectivity (0-1)</param>
    /// <param name="totalVectors">Total number of vectors in the collection</param>
    /// <returns>Optimal number of candidates to search</returns>
    private static int CalculateOptimalExpansion(int k, double selectivity, int totalVectors)
    {
        if (selectivity <= 0.0) return totalVectors; // No matches expected, search all

        // Calculate expansion factor based on selectivity with safety margin
        double safetyMargin = 1.5; // 50% safety margin
        double requiredExpansion = (k / selectivity) * safetyMargin;

        // Apply reasonable bounds
        int minExpansion = k * 2;     // At least 2x expansion
        int maxExpansion = k * 20;    // At most 20x expansion

        int expansion = (int)Math.Ceiling(Math.Max(minExpansion, Math.Min(maxExpansion, requiredExpansion)));

        return Math.Min(expansion, totalVectors);
    }
}
