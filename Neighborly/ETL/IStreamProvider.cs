namespace Neighborly.ETL;

/// <summary>
/// Provides streams for ETL operations with support for decorators (compression, encryption, etc.)
/// </summary>
internal interface IStreamProvider
{
    /// <summary>
    /// Creates a read stream for the specified path
    /// </summary>
    /// <param name="path">Path to the file to read</param>
    /// <returns>A stream configured for reading</returns>
    Stream CreateReadStream(string path);

    /// <summary>
    /// Creates a write stream for the specified path
    /// </summary>
    /// <param name="path">Path to the file to write</param>
    /// <returns>A stream configured for writing</returns>
    Stream CreateWriteStream(string path);
}
