namespace Neighborly;

/// <summary>
/// Locality Sensitive Hashing (LSH) search method.
/// A hash function that maps similar input items to the same "bucket" with high probability.
/// </summary>
public class LSHSearch : ISearchMethod
{
    public IList<Vector> Search(IList<Vector> vectors, Vector query, int k)
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
