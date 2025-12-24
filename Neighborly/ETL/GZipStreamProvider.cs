using System.IO.Compression;

namespace Neighborly.ETL;

/// <summary>
/// Stream provider decorator that adds GZip compression/decompression
/// </summary>
internal sealed class GZipStreamProvider : IStreamProvider
{
    private readonly IStreamProvider _innerProvider;

    /// <summary>
    /// Creates a new GZip stream provider that wraps another stream provider
    /// </summary>
    /// <param name="innerProvider">The underlying stream provider to wrap</param>
    public GZipStreamProvider(IStreamProvider innerProvider)
    {
        _innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));
    }

    /// <inheritdoc />
    public Stream CreateReadStream(string path)
    {
        var baseStream = _innerProvider.CreateReadStream(path);
        return new GZipStream(baseStream, CompressionMode.Decompress, leaveOpen: false);
    }

    /// <inheritdoc />
    public Stream CreateWriteStream(string path)
    {
        var baseStream = _innerProvider.CreateWriteStream(path);
        return new GZipStream(baseStream, CompressionLevel.Fastest, leaveOpen: false);
    }
}
