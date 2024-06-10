namespace Neighborly.Search;

/// <summary>
/// Locality Sensitive Hashing (LSH) search method.
/// A hash function that maps similar input items to the same "bucket" with high probability.
/// </summary>
public class LSHSearch
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
