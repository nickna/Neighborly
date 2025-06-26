using System.Linq;
using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Batch-optimized linear search implementation that processes distance calculations in efficient batches.
/// </summary>
public class BatchOptimizedLinearSearch
{
    private readonly IDistanceCalculator _distanceCalculator;
    
    public BatchOptimizedLinearSearch(IDistanceCalculator? distanceCalculator = null)
    {
        _distanceCalculator = distanceCalculator ?? BatchEuclideanDistanceCalculator.Instance;
    }
    
    public IList<Vector> Search(VectorList vectors, Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(query);

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        if (vectors.Count == 0)
        {
            return new List<Vector>();
        }

        // Use batch distance calculation if supported
        float[] distances;
        if (_distanceCalculator is IBatchDistanceCalculator batchCalculator)
        {
            // Convert VectorList to IList<Vector> for batch processing
            var vectorsList = vectors.ToList();
            distances = batchCalculator.CalculateDistances(query, vectorsList);
        }
        else
        {
            // Fallback to sequential calculation with the extension method
            var vectorsList = vectors.ToList();
            distances = query.BatchDistance(vectorsList, _distanceCalculator);
        }

        // Create indexed results for sorting
        var indexedDistances = new (int index, float distance)[vectors.Count];
        for (int i = 0; i < distances.Length; i++)
        {
            indexedDistances[i] = (i, distances[i]);
        }

        // Sort by distance and take k nearest
        var kNearest = indexedDistances
            .OrderBy(x => x.distance)
            .Take(k)
            .Select(x => vectors[x.index])
            .ToList();

        return kNearest;
    }

    /// <summary>
    /// Performs a parallel batch search for very large datasets.
    /// </summary>
    public IList<Vector> ParallelSearch(VectorList vectors, Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(query);

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        if (vectors.Count == 0)
        {
            return new List<Vector>();
        }

        // Use parallel batch distance calculation
        var vectorsList = vectors.ToList();
        float[] distances = query.ParallelBatchDistance(vectorsList, _distanceCalculator);

        // Create indexed results for sorting
        var indexedDistances = new (int index, float distance)[vectors.Count];
        for (int i = 0; i < distances.Length; i++)
        {
            indexedDistances[i] = (i, distances[i]);
        }

        // Sort by distance and take k nearest
        var kNearest = indexedDistances
            .OrderBy(x => x.distance)
            .Take(k)
            .Select(x => vectors[x.index])
            .ToList();

        return kNearest;
    }
}