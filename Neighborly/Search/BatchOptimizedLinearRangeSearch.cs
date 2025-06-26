using Neighborly.Distance;
using System.Linq;

namespace Neighborly.Search;

/// <summary>
/// Batch-optimized linear range search that processes distance calculations efficiently.
/// </summary>
public class BatchOptimizedLinearRangeSearch
{
    /// <summary>
    /// Performs batch-optimized range search to find all vectors within the specified radius.
    /// </summary>
    public static IList<Vector> Search(VectorList vectors, Vector query, float radius, IDistanceCalculator? distanceCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(query);

        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
        }

        if (vectors.Count == 0)
        {
            return new List<Vector>();
        }

        distanceCalculator ??= BatchEuclideanDistanceCalculator.Instance;

        // Use batch distance calculation
        float[] distances;
        var vectorsList = vectors.ToList();
        
        if (distanceCalculator is IBatchDistanceCalculator batchCalculator)
        {
            distances = batchCalculator.CalculateDistances(query, vectorsList);
        }
        else
        {
            distances = query.BatchDistance(vectorsList, distanceCalculator);
        }

        // Filter by radius and create indexed results
        var withinRadius = new List<(int index, float distance)>();
        for (int i = 0; i < distances.Length; i++)
        {
            if (distances[i] <= radius)
            {
                withinRadius.Add((i, distances[i]));
            }
        }

        // Sort by distance and return vectors
        var sortedResults = withinRadius
            .OrderBy(x => x.distance)
            .Select(x => vectors[x.index])
            .ToList();

        return sortedResults;
    }

    /// <summary>
    /// Performs parallel batch-optimized range search for very large datasets.
    /// </summary>
    public static IList<Vector> ParallelSearch(VectorList vectors, Vector query, float radius, IDistanceCalculator? distanceCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(query);

        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
        }

        if (vectors.Count == 0)
        {
            return new List<Vector>();
        }

        distanceCalculator ??= BatchEuclideanDistanceCalculator.Instance;

        // Use parallel batch distance calculation
        var vectorsList = vectors.ToList();
        float[] distances = query.ParallelBatchDistance(vectorsList, distanceCalculator);

        // Filter by radius in parallel
        var withinRadius = new System.Collections.Concurrent.ConcurrentBag<(int index, float distance)>();
        
        Parallel.For(0, distances.Length, i =>
        {
            if (distances[i] <= radius)
            {
                withinRadius.Add((i, distances[i]));
            }
        });

        // Sort by distance and return vectors
        var sortedResults = withinRadius
            .OrderBy(x => x.distance)
            .Select(x => vectors[x.index])
            .ToList();

        return sortedResults;
    }
}