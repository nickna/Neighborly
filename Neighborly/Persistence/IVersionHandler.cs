using System.Collections.Concurrent;

namespace Neighborly.Persistence;

/// <summary>
/// Handles loading a specific database file format version.
/// Implementations should follow the compositional pattern where newer versions
/// build upon older versions (e.g., V1 = V0 + indexes).
/// </summary>
internal interface IVersionHandler
{
    /// <summary>
    /// Gets the file format version number this handler supports.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Loads vectors into the target dictionary without holding any locks (Phase 1 of load operation).
    /// This method performs background deserialization into a temporary dictionary.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="targetDict">The concurrent dictionary to load vectors into.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Load result containing vector count and whether indexes need rebuilding.</returns>
    Task<LoadResult> LoadVectorsAsync(
        BinaryReader reader,
        ConcurrentDictionary<Guid, Vector> targetDict,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads search indexes after vectors have been swapped into the database (Phase 3 of load operation).
    /// This method is called without holding any locks, after the atomic dictionary swap.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="searchService">The search service to load indexes into.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task LoadIndexesAsync(
        BinaryReader reader,
        Search.SearchService searchService,
        CancellationToken cancellationToken);
}
