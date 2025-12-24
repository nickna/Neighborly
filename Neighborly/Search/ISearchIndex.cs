namespace Neighborly.Search;

/// <summary>
/// Base interface for all search algorithms in the Neighborly vector database.
/// Provides k-nearest neighbor search capability.
/// </summary>
public interface ISearchIndex
{
    /// <summary>
    /// Performs k-nearest neighbor search to find the k vectors closest to the query.
    /// </summary>
    /// <param name="query">The query vector to search for</param>
    /// <param name="k">The number of nearest neighbors to return</param>
    /// <returns>
    /// A list of the k nearest vectors, ordered by distance from the query (closest first).
    /// May return fewer than k results if the dataset contains fewer than k vectors.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when query is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when k is less than or equal to 0</exception>
    IList<Vector> Search(Vector query, int k);
}
