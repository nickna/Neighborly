namespace Neighborly.Search;

/// <summary>
/// Interface for search indexes that support persistence to and from binary storage.
/// Extends IBuildableSearchIndex to add serialization capabilities for saving/loading
/// pre-built indexes to avoid expensive rebuild operations.
/// </summary>
public interface ISerializableSearchIndex : IBuildableSearchIndex
{
    /// <summary>
    /// Saves the index to a binary writer for persistence.
    /// The index should be built before calling this method.
    /// </summary>
    /// <param name="writer">The binary writer to serialize to</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when writer is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when index is not built</exception>
    Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the index from a binary reader.
    /// After loading, IsBuilt should return true and the index should be ready for searches.
    /// </summary>
    /// <param name="reader">The binary reader to deserialize from</param>
    /// <param name="vectors">The vector list to resolve vector references during deserialization</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation</param>
    /// <returns>A task representing the asynchronous load operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when reader or vectors is null</exception>
    /// <exception cref="InvalidDataException">Thrown when the data format is invalid or unsupported</exception>
    Task LoadAsync(BinaryReader reader, VectorList vectors, CancellationToken cancellationToken = default);

    /// <summary>
    /// The file format version that this implementation writes.
    /// Used for version compatibility checking during deserialization.
    /// </summary>
    int FileFormatVersion { get; }
}
