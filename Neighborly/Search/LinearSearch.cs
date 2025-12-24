using System.Linq;

namespace Neighborly.Search;

/// <summary>
/// Linear search method.
/// </summary>
public class LinearSearch : ISearchIndex
{
    private readonly VectorList _vectors;

    public LinearSearch(VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        _vectors = vectors;
    }

    /// <summary>
    /// Instance method for searching the vectors.
    /// </summary>
    public IList<Vector> Search(Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        if (_vectors.Count == 0)
        {
            return [];
        }

        // Calculate distances for all vectors and sort by distance
        var vectorDistances = new List<(Vector vector, float distance)>();

        for (int i = 0; i < _vectors.Count; i++)
        {
            float distance = _vectors[i].Distance(query);
            vectorDistances.Add((_vectors[i], distance));
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

