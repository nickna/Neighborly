namespace Neighborly.Search;

/// <summary>
/// Interface for search algorithms that require an index building phase before searching.
/// Examples: KDTree, BallTree, HNSW, LSH.
/// Extends ISearchIndex to provide both building and searching capabilities.
/// </summary>
public interface IBuildableSearchIndex : ISearchIndex
{
    /// <summary>
    /// Builds the search index from a collection of vectors.
    /// This operation may be computationally expensive and is typically performed once
    /// before multiple search operations.
    /// </summary>
    /// <param name="vectors">The vectors to index</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation</param>
    /// <returns>A task representing the asynchronous build operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when vectors is null</exception>
    Task BuildAsync(VectorList vectors, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the index has been built and is ready for search operations.
    /// Search operations may fail or return incorrect results if called before building.
    /// </summary>
    bool IsBuilt { get; }
}
