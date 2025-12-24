using System.Collections.Concurrent;

namespace Neighborly.ETL;

public abstract class EtlBase : IETL
{
    /// <summary>
    /// Maximum degree of parallelism for directory imports (default: 4)
    /// Can be overridden by derived classes for format-specific tuning
    /// </summary>
    protected virtual int MaxDegreeOfParallelism => 4;

    /// <summary>
    /// Gets the stream provider for this ETL implementation
    /// </summary>
    private protected abstract IStreamProvider StreamProvider { get; }

    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public Task ImportDataAsync(string path, ICollection<Vector> vectors, bool isDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(vectors);

        return isDirectory
            ? ImportDirectoryAsync(path, vectors, cancellationToken)
            : ImportFileAsync(path, vectors, cancellationToken);
    }

    /// <summary>
    /// Imports vectors from a single file
    /// </summary>
    protected abstract Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken);

    /// <summary>
    /// Imports vectors from all files in a directory (parallel processing)
    /// </summary>
    private async Task ImportDirectoryAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(path, $"*{FileExtension}").ToArray();

        if (files.Length == 0)
        {
            return; // No files to process
        }

        var exceptions = new ConcurrentBag<Exception>();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (file, ct) =>
            {
                try
                {
                    // Each file gets its own local collection
                    var localVectors = new List<Vector>();
                    await ImportFileAsync(file, localVectors, ct).ConfigureAwait(false);

                    // Batch add to shared collection
                    // VectorList is thread-safe (ConcurrentDictionary internally)
                    foreach (var vector in localVectors)
                    {
                        vectors.Add(vector);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    exceptions.Add(new InvalidOperationException($"Failed to import {file}", ex));
                }
            }).ConfigureAwait(false);

        if (!exceptions.IsEmpty)
        {
            throw new AggregateException("One or more files failed to import", exceptions);
        }
    }

    /// <inheritdoc />
    public abstract Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task ExportDataAsync(IAsyncEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default)
    {
        // Default implementation materializes to list (derived classes can optimize)
        var list = new List<Vector>();
        await foreach (var vector in vectors.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            list.Add(vector);
        }
        await ExportDataAsync((IEnumerable<Vector>)list, path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Helper to create read stream using the stream provider
    /// </summary>
    protected Stream CreateReadStream(string path) => StreamProvider.CreateReadStream(path);

    /// <summary>
    /// Helper to create write stream using the stream provider
    /// </summary>
    protected Stream CreateWriteStream(string path) => StreamProvider.CreateWriteStream(path);
}
