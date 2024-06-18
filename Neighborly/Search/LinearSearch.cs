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

        List<Vector> results = new List<Vector>(k);

        for (int i = 0; i < vectors.Count; i++)
        {
            if (results.Count >= k)
                break;

            if (vectors[i].Equals(query))
                results.Add(vectors[i]);
        }

        return results;
    }
}

