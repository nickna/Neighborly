namespace Neighborly;

/// <summary>
/// Represents a transaction scope for batched vector database operations.
/// Implements read-your-writes semantics within the transaction.
/// </summary>
/// <remarks>
/// Operations are buffered in memory during the transaction and applied atomically
/// on commit. The database write lock is only acquired during commit, allowing
/// concurrent reads during the transaction.
/// </remarks>
public interface IVectorTransaction : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this transaction.
    /// </summary>
    Guid TransactionId { get; }

    /// <summary>
    /// Gets whether this transaction has been committed.
    /// </summary>
    bool IsCommitted { get; }

    /// <summary>
    /// Gets whether this transaction has been rolled back or disposed without commit.
    /// </summary>
    bool IsRolledBack { get; }

    /// <summary>
    /// Gets the number of pending operations in this transaction.
    /// </summary>
    int PendingOperationCount { get; }

    /// <summary>
    /// Adds a vector to the transaction buffer.
    /// </summary>
    /// <param name="vector">The vector to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if vector is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if transaction is not active.</exception>
    void Add(Vector vector);

    /// <summary>
    /// Updates a vector in the transaction buffer.
    /// If the vector was added in this transaction, updates the buffered version.
    /// If the vector exists in the database, marks it for update at commit time.
    /// </summary>
    /// <param name="id">The ID of the vector to update.</param>
    /// <param name="vector">The new vector data.</param>
    /// <returns>True if the vector exists (in buffer or database), false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown if vector is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if transaction is not active.</exception>
    bool Update(Guid id, Vector vector);

    /// <summary>
    /// Marks a vector for removal.
    /// If the vector was added in this transaction, removes it from the buffer.
    /// If the vector exists in the database, marks it for deletion at commit time.
    /// </summary>
    /// <param name="id">The ID of the vector to remove.</param>
    /// <returns>True if the vector was found, false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if transaction is not active.</exception>
    bool Remove(Guid id);

    /// <summary>
    /// Gets a vector by ID, checking transaction buffer first (read-your-writes).
    /// </summary>
    /// <param name="id">The ID of the vector to retrieve.</param>
    /// <returns>The vector if found in buffer or database (and not deleted), null otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if transaction is not active.</exception>
    Vector? GetVector(Guid id);

    /// <summary>
    /// Commits all buffered operations atomically to the database.
    /// Acquires write lock only during this operation.
    /// </summary>
    /// <exception cref="TransactionException">Thrown if commit fails due to conflicts or other errors.</exception>
    /// <exception cref="InvalidOperationException">Thrown if already committed or rolled back.</exception>
    void Commit();

    /// <summary>
    /// Commits all buffered operations atomically to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TransactionException">Thrown if commit fails due to conflicts or other errors.</exception>
    /// <exception cref="InvalidOperationException">Thrown if already committed or rolled back.</exception>
    /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards all buffered operations. Does not affect the database.
    /// </summary>
    void Rollback();
}
