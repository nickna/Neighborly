using Microsoft.Extensions.Logging;

namespace Neighborly;

/// <summary>
/// Represents a transaction for batched vector database operations.
/// Thread-safe for single-threaded transaction usage.
/// </summary>
public sealed class VectorTransaction : IVectorTransaction
{
    private readonly VectorDatabase _database;
    private readonly ILogger? _logger;

    // Transaction state constants
    private const int StateActive = 0;
    private const int StateCommitted = 1;
    private const int StateRolledBack = 2;

    private int _state = StateActive;
    private bool _disposed;

    // Buffers for operations (read-your-writes)
    private readonly Dictionary<Guid, Vector> _addedVectors = new();
    private readonly Dictionary<Guid, Vector> _updatedVectors = new();
    private readonly HashSet<Guid> _deletedIds = new();

    /// <inheritdoc />
    public Guid TransactionId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public bool IsCommitted => Interlocked.CompareExchange(ref _state, 0, 0) == StateCommitted;

    /// <inheritdoc />
    public bool IsRolledBack => Interlocked.CompareExchange(ref _state, 0, 0) == StateRolledBack;

    /// <inheritdoc />
    public int PendingOperationCount => _addedVectors.Count + _updatedVectors.Count + _deletedIds.Count;

    /// <summary>
    /// Creates a new transaction for the specified database.
    /// </summary>
    /// <param name="database">The database this transaction operates on.</param>
    /// <param name="logger">Optional logger for transaction operations.</param>
    internal VectorTransaction(VectorDatabase database, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
        _logger = logger;
        _logger?.LogDebug("Transaction {TransactionId} started.", TransactionId);
    }

    /// <inheritdoc />
    public void Add(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfNotActive();

        // If previously deleted in this transaction AND the vector doesn't exist in DB,
        // remove from deleted set. If it exists in DB, keep the delete so the original
        // is removed before the new one is added.
        if (_deletedIds.Contains(vector.Id) && _database.GetVector(vector.Id) == null)
        {
            _deletedIds.Remove(vector.Id);
        }

        // If previously marked for update, remove (add supersedes update)
        _updatedVectors.Remove(vector.Id);

        _addedVectors[vector.Id] = vector;
    }

    /// <inheritdoc />
    public bool Update(Guid id, Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfNotActive();

        // If deleted in this transaction, can't update
        if (_deletedIds.Contains(id))
        {
            return false;
        }

        // If added in this transaction, update the buffered add
        if (_addedVectors.ContainsKey(id))
        {
            var updatedVector = new Vector(vector.Values, vector.OriginalText);
            updatedVector.Id = id;
            _addedVectors[id] = updatedVector;
            return true;
        }

        // If already marked for update, update the buffered update
        if (_updatedVectors.ContainsKey(id))
        {
            var updatedVector = new Vector(vector.Values, vector.OriginalText);
            updatedVector.Id = id;
            _updatedVectors[id] = updatedVector;
            return true;
        }

        // Check if exists in database
        var existing = _database.GetVector(id);
        if (existing == null)
        {
            return false;
        }

        var newVector = new Vector(vector.Values, vector.OriginalText);
        newVector.Id = id;
        _updatedVectors[id] = newVector;
        return true;
    }

    /// <inheritdoc />
    public bool Remove(Guid id)
    {
        ThrowIfNotActive();

        // If added in this transaction, just remove from buffer
        if (_addedVectors.Remove(id))
        {
            return true;
        }

        // If updated in this transaction, remove from updates
        _updatedVectors.Remove(id);

        // Check if already marked for deletion
        if (_deletedIds.Contains(id))
        {
            return false;
        }

        // Check if exists in database
        var existing = _database.GetVector(id);
        if (existing == null)
        {
            return false;
        }

        _deletedIds.Add(id);
        return true;
    }

    /// <inheritdoc />
    public Vector? GetVector(Guid id)
    {
        ThrowIfNotActive();

        // Check added vectors first (read-your-writes)
        // This takes precedence over deletions (for delete-then-add case)
        if (_addedVectors.TryGetValue(id, out var addedVector))
        {
            return addedVector;
        }

        // Check if deleted in this transaction
        if (_deletedIds.Contains(id))
        {
            return null;
        }

        // Check updated vectors (read-your-writes)
        if (_updatedVectors.TryGetValue(id, out var updatedVector))
        {
            return updatedVector;
        }

        // Fall back to database
        return _database.GetVector(id);
    }

    /// <inheritdoc />
    public void Commit()
    {
        ThrowIfNotActive();

        if (_addedVectors.Count == 0 && _updatedVectors.Count == 0 && _deletedIds.Count == 0)
        {
            // Nothing to commit
            Interlocked.Exchange(ref _state, StateCommitted);
            _logger?.LogDebug("Transaction {TransactionId} committed (no operations).", TransactionId);
            return;
        }

        // Prepare operation lists
        var adds = _addedVectors.Values.ToList();
        var updates = _updatedVectors.ToList();
        var deletes = _deletedIds.ToList();

        // Commit to database (this acquires the write lock)
        _database.CommitTransaction(adds, updates, deletes, this);

        Interlocked.Exchange(ref _state, StateCommitted);
        _logger?.LogDebug(
            "Transaction {TransactionId} committed: {Adds} adds, {Updates} updates, {Deletes} deletes.",
            TransactionId, adds.Count, updates.Count, deletes.Count);

        ClearBuffers();
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNotActive();
        cancellationToken.ThrowIfCancellationRequested();

        // For MVP, commit is synchronous internally (lock duration is brief)
        await Task.Run(() => Commit(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Rollback()
    {
        if (Interlocked.CompareExchange(ref _state, StateRolledBack, StateActive) == StateActive)
        {
            ClearBuffers();
            _logger?.LogDebug("Transaction {TransactionId} rolled back.", TransactionId);
        }
    }

    private void ClearBuffers()
    {
        _addedVectors.Clear();
        _updatedVectors.Clear();
        _deletedIds.Clear();
    }

    private void ThrowIfNotActive()
    {
        var currentState = Interlocked.CompareExchange(ref _state, 0, 0);
        if (currentState == StateCommitted)
        {
            throw new InvalidOperationException("Transaction has already been committed.");
        }
        if (currentState == StateRolledBack)
        {
            throw new InvalidOperationException("Transaction has been rolled back.");
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Implicit rollback if not committed
        if (Interlocked.CompareExchange(ref _state, StateRolledBack, StateActive) == StateActive)
        {
            ClearBuffers();
            _logger?.LogDebug("Transaction {TransactionId} disposed without commit (implicit rollback).", TransactionId);
        }
    }
}
