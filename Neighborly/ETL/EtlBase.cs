namespace Neighborly.ETL;

public abstract class EtlBase : IETL
{
    /// <inheritdoc />
    public bool IsDirectory { get; set; }

    /// <inheritdoc />
    public abstract string FileExtension { get; }

    /// <inheritdoc />
    public abstract Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task ImportDataAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(vectors);

        if (IsDirectory)
        {
            return ImportDirectoryAsync(path, vectors, cancellationToken);
        }
        else
        {
            return ImportFileAsync(path, vectors, cancellationToken);
        }
    }

    protected abstract Task ImportFileAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken);

    private async Task ImportDirectoryAsync(string path, ICollection<Vector> vectors, CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(path, $"*{FileExtension}"))
        {
            await ImportFileAsync(file, vectors, cancellationToken);
        }
    }


    protected virtual Stream CreateWriteStream(string path) => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

    protected virtual Stream CreateReadStream(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
}
