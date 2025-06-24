using Neighborly.Distance;
using System.Linq;

namespace Neighborly.Search;

/// <summary>
/// Linear range search method that finds all vectors within a specified distance radius.
/// </summary>
public class LinearRangeSearch
{
    /// <summary>
    /// Performs range search to find all vectors within the specified radius of the query vector.
    /// </summary>
    /// <param name="vectors">The vector list to search in</param>
    /// <param name="query">The query vector</param>
    /// <param name="radius">The maximum distance from the query vector</param>
    /// <param name="distanceCalculator">The distance calculator to use (defaults to Euclidean)</param>
    /// <returns>A list of vectors within the specified radius, ordered by distance (closest first)</returns>
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

        distanceCalculator ??= EuclideanDistanceCalculator.Instance;

        // Calculate distances for all vectors and filter by radius
        var vectorDistances = new List<(Vector vector, float distance)>();
        
        for (int i = 0; i < vectors.Count; i++)
        {
            float distance = distanceCalculator.CalculateDistance(vectors[i], query);
            if (distance <= radius)
            {
                vectorDistances.Add((vectors[i], distance));
            }
        }

        // Sort by distance (ascending) and return the vectors
        var sortedResults = vectorDistances
            .OrderBy(vd => vd.distance)
            .Select(vd => vd.vector)
            .ToList();

        return sortedResults;
    }
}