namespace Neighborly.ETL;

/// <summary>
/// Default stream provider using FileStream with async I/O enabled
/// </summary>
internal sealed class FileStreamProvider : IStreamProvider
{
    /// <summary>
    /// Singleton instance of the file stream provider
    /// </summary>
    public static readonly FileStreamProvider Instance = new();

    private FileStreamProvider() { }

    /// <inheritdoc />
    public Stream CreateReadStream(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

    /// <inheritdoc />
    public Stream CreateWriteStream(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
}
