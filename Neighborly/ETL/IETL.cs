namespace Neighborly.ETL;

/// <summary>
/// VectorDatabase Interface for Extract Transform and Load (ETL) operations for importing and exporting Vector data.
/// </summary>
public interface IETL
{
    /// <summary>
    /// Gets the file extension for this ETL format (e.g., ".csv", ".json")
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Imports vector data from a file or directory
    /// </summary>
    /// <param name="path">Path to file or directory</param>
    /// <param name="vectors">Collection to populate with imported vectors</param>
    /// <param name="isDirectory">True if path is a directory containing multiple files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ImportDataAsync(string path, ICollection<Vector> vectors, bool isDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports vector data to a file (synchronous enumeration)
    /// </summary>
    /// <param name="vectors">Vectors to export</param>
    /// <param name="path">Output file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportDataAsync(IEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports vector data to a file (streaming with async enumeration)
    /// </summary>
    /// <param name="vectors">Vectors to export as async stream</param>
    /// <param name="path">Output file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportDataAsync(IAsyncEnumerable<Vector> vectors, string path, CancellationToken cancellationToken = default);
}
