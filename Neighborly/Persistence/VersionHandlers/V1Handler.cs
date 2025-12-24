using System.Collections.Concurrent;

namespace Neighborly.Persistence.VersionHandlers;

/// <summary>
/// Handles V1 file format: V0 format + serialized search indexes.
/// Demonstrates composition over duplication - delegates vector loading to V0Handler.
/// </summary>
internal sealed class V1Handler : IVersionHandler
{
    private readonly V0Handler _v0Handler = new();

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public async Task<LoadResult> LoadVectorsAsync(
        BinaryReader reader,
        ConcurrentDictionary<Guid, Vector> targetDict,
        CancellationToken cancellationToken)
    {
        // V1 vector format is identical to V0 - delegate to avoid duplication
        var result = await _v0Handler.LoadVectorsAsync(reader, targetDict, cancellationToken)
            .ConfigureAwait(false);

        // V1 has serialized indexes, so they don't need rebuilding
        return new LoadResult(result.VectorCount, IndexesNeedRebuild: false);
    }

    /// <inheritdoc />
    public async Task LoadIndexesAsync(
        BinaryReader reader,
        Search.SearchService searchService,
        CancellationToken cancellationToken)
    {
        // V1 format includes serialized indexes after the vector data
        await searchService.LoadAsync(reader, cancellationToken).ConfigureAwait(false);
    }
}
