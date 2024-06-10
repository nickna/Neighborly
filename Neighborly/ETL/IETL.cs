namespace Neighborly.ETL;

/// <summary>
/// VectorDatabase Interface for Extract Transform and Load (ETL) operations for importing and exporting Vector data.
/// </summary>
public interface IETL
{
    /// <summary>
    /// Indicates if the ETL operation should be performed on a directory or a file.
    /// </summary>
    bool IsDirectory { get; set; }
    string FileExtension { get; }
    public Task ImportDataAsync(string path, ICollection<Vector> vectors,  CancellationToken cancellationToken = default);
    public Task ExportDataAsync(IEnumerable<Vector> vectors, string path,  CancellationToken cancellationToken = default);
}
