using System.Collections.Concurrent;

namespace Neighborly.Persistence.VersionHandlers;

/// <summary>
/// Handles V0 file format: vector count + vectors (no serialized indexes).
/// This is the base format that all subsequent versions build upon.
/// </summary>
internal sealed class V0Handler : IVersionHandler
{
    /// <inheritdoc />
    public int Version => 0;

    /// <inheritdoc />
    public Task<LoadResult> LoadVectorsAsync(
        BinaryReader reader,
        ConcurrentDictionary<Guid, Vector> targetDict,
        CancellationToken cancellationToken)
    {
        var vectorCount = reader.ReadInt32();

        // Sanity check: vector count should be reasonable
        if (vectorCount < 0 || vectorCount > 100_000_000)
            throw new InvalidDataException($"Invalid vector count in file: {vectorCount}");

        for (int i = 0; i < vectorCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextVectorSize = reader.ReadInt32();

            // Sanity check: vector size should be reasonable
            if (nextVectorSize <= 0 || nextVectorSize > 100_000_000)
                throw new InvalidDataException($"Invalid vector size at index {i}: {nextVectorSize}");

            var bytes = reader.ReadBytes(nextVectorSize);

            // Validate that we got all expected bytes (detects truncated files)
            if (bytes.Length != nextVectorSize)
                throw new InvalidDataException(
                    $"Truncated file: expected {nextVectorSize} bytes for vector {i}, got {bytes.Length}");

            var vector = new Vector(bytes);
            targetDict.TryAdd(vector.Id, vector);
        }

        // V0 format has no serialized indexes, so they need to be rebuilt
        return Task.FromResult(new LoadResult(vectorCount, IndexesNeedRebuild: true));
    }

    /// <inheritdoc />
    public Task LoadIndexesAsync(
        BinaryReader reader,
        Search.SearchService searchService,
        CancellationToken cancellationToken)
    {
        // V0 format has no serialized indexes - nothing to load
        return Task.CompletedTask;
    }
}
