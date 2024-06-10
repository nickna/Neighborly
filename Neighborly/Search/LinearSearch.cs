namespace Neighborly.Search;

/// <summary>
/// Linear search method.
/// </summary>
public class LinearSearch
{
    public static IList<Vector> Search(VectorList vectors, Vector query, int k)
    {
        List<Vector> results = new List<Vector>();

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

