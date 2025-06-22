using System.Linq;

namespace Neighborly.Search;

/// <summary>
/// Linear search method.
/// </summary>
public class LinearSearch
{
    public static IList<Vector> Search(VectorList vectors, Vector query, int k)
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

        // Calculate distances for all vectors and sort by distance
        var vectorDistances = new List<(Vector vector, float distance)>();
        
        for (int i = 0; i < vectors.Count; i++)
        {
            float distance = vectors[i].Distance(query);
            vectorDistances.Add((vectors[i], distance));
        }

        // Sort by distance (ascending) and take the k nearest neighbors
        var sortedResults = vectorDistances
            .OrderBy(vd => vd.distance)
            .Take(k)
            .Select(vd => vd.vector)
            .ToList();

        return sortedResults;
    }
}

