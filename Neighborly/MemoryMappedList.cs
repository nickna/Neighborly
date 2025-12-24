using System.Buffers;
using System.Collections;

namespace Neighborly;

public class MemoryMappedList : IDisposable, IEnumerable<Vector>, IAsyncEnumerable<Vector>
{
    private const int s_idBytesLength = 16;
    private const int s_offsetBytesLength = sizeof(long);
    private const int s_lengthBytesLength = sizeof(int);
    private const int s_indexEntryByteLength = s_idBytesLength + s_offsetBytesLength + s_lengthBytesLength;
    private static readonly Guid s_tombStone;
    private static readonly byte[] s_tombStoneBytes;

    // Public constants for corruption detection
    internal const int IdBytesLength = s_idBytesLength;
    internal const int IndexEntryByteLength = s_indexEntryByteLength;
    internal static Guid TombStone => s_tombStone;
    private readonly RandomAccessFileHolder _indexFile;
    private readonly RandomAccessFileHolder _dataFile;
    private readonly WriteAheadLog _wal;
    private readonly CheckpointManager _checkpointManager;

    // Single lock for all synchronization - uses .NET 9+ Lock class for efficiency
    private readonly Lock _lock = new();
    // Async-compatible lock for async operations
    private readonly SemaphoreSlim _asyncLock = new(1, 1);

    private long _count;
    private volatile bool _disposedValue;

    // Append position tracking - protected by _lock
    private long _nextIndexPosition;
    private long _nextDataPosition;

    private long _defragIndexPosition;
    private long _newDataPosition;
    private const int _defragBatchSize = 100; // Number of entries to defrag in one batch, adjust based on performance needs

  
    /// <summary>
    /// Flush the memory-mapped files to disk and checkpoint the WAL.
    /// </summary>
    public void Flush()
    {
        ThrowIfDisposed();

        using (_lock.EnterScope())
        {
            _checkpointManager.ForceCheckpoint();
        }
    }

    /// <summary>
    /// Asynchronously flush the memory-mapped files to disk and checkpoint the WAL.
    /// </summary>
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _checkpointManager.ForceCheckpointAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    static MemoryMappedList()
    {
        s_tombStone = Guid.NewGuid();

        Span<byte> tombStoneBytes = stackalloc byte[16];
        if (!s_tombStone.TryWriteBytes(tombStoneBytes))
        {
            throw new InvalidOperationException("Failed to write the tombstone to bytes");
        }

        s_tombStoneBytes = tombStoneBytes.ToArray();
    }

    /// <summary>
    /// Creates a new memory-mapped list with dynamic file growth.
    /// Files start small and grow automatically as needed.
    /// </summary>
    /// <param name="flushPolicy">The flush policy for WAL checkpoints.</param>
    /// <param name="enableGrowth">If true, files will grow automatically. If false, uses fixed capacity.</param>
    public MemoryMappedList(FlushPolicy flushPolicy = FlushPolicy.Batched, bool enableGrowth = true)
    {
        if (enableGrowth)
        {
            // Dynamic growth mode - start small
            _indexFile = new RandomAccessFileHolder(FileGrowthStrategy.ForIndexFile());
            _dataFile = new RandomAccessFileHolder(FileGrowthStrategy.ForDataFile());
        }
        else
        {
            // Legacy fixed mode - use reasonable defaults
            long capacity = 10000; // Support 10K vectors initially
            _indexFile = new RandomAccessFileHolder(s_indexEntryByteLength * capacity);
            _dataFile = new RandomAccessFileHolder(4096L * capacity);
        }

        _wal = new WriteAheadLog(_indexFile.Filename);

        // Create CheckpointManager with WAL reference for coordinated durability
        _checkpointManager = new CheckpointManager(_wal, flushPolicy);

        // Register files with checkpoint manager
        _checkpointManager.RegisterFile(_indexFile);
        _checkpointManager.RegisterFile(_dataFile);

        // Initialize defrag tracking
        _newDataPosition = 0;

        // Cleanup orphaned temp files from crashed resize operations
        CleanupOrphanedTempFiles();

        // Recovery on startup
        RecoverFromWAL();

        // Initialize append metadata after recovery
        InitializeAppendMetadata();
    }

    /// <summary>
    /// Creates a new memory-mapped list with fixed capacity (backward compatibility).
    /// </summary>
    /// <param name="capacity">The number of vectors to pre-allocate space for.</param>
    /// <param name="flushPolicy">The flush policy for WAL checkpoints.</param>
    public MemoryMappedList(long capacity, FlushPolicy flushPolicy = FlushPolicy.Batched)
    {
        _indexFile = new RandomAccessFileHolder(s_indexEntryByteLength * capacity);
        // Based on typical vector dimensions, 4096 bytes should be enough for most cases as of 2024-06
        _dataFile = new RandomAccessFileHolder(4096L * capacity);
        _wal = new WriteAheadLog(_indexFile.Filename);

        // Create CheckpointManager with WAL reference for coordinated durability
        _checkpointManager = new CheckpointManager(_wal, flushPolicy);

        // Register files with checkpoint manager
        _checkpointManager.RegisterFile(_indexFile);
        _checkpointManager.RegisterFile(_dataFile);

        // Initialize defrag tracking
        _newDataPosition = 0;

        // Cleanup orphaned temp files from crashed resize operations
        CleanupOrphanedTempFiles();

        // Recovery on startup
        RecoverFromWAL();

        // Initialize append metadata after recovery
        InitializeAppendMetadata();
    }


    public long Count
    {
        get
        {
            ThrowIfDisposed();
            return Interlocked.Read(ref _count);
        }
    }

#pragma warning disable CA1822 // Mark members as static - kept as instance member for API consistency with collection patterns
    public bool IsReadOnly => false;
#pragma warning restore CA1822

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Wait for any active operations to complete
                using (_lock.EnterScope())
                {
                    // Dispose in reverse order of creation
                    // CheckpointManager will perform final checkpoint before disposal
                    _checkpointManager?.Dispose();
                    _wal?.Dispose();
                    _dataFile?.Dispose();
                    _indexFile?.Dispose();
                }

                _asyncLock.Dispose();
            }

            _disposedValue = true;
        }
    }

    ~MemoryMappedList()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Vector? GetVector(long index)
    {
        ThrowIfDisposed();

        if (index < 0L || index >= Interlocked.Read(ref _count))
        {
            return null;
        }

        using (_lock.EnterScope())
        {
            if (!TryFindVectorByIndex(index, out var result))
                return null;

            return ReadVectorAtLocation((result.IndexPosition, result.DataOffset, result.Length));
        }
    }

    public Vector? GetVector(Guid id)
    {
        ThrowIfDisposed();

        using (_lock.EnterScope())
        {
            if (!TryFindVectorById(id, out var result))
                return null;

            return ReadVectorAtLocation((result.IndexPosition, result.DataOffset, result.Length));
        }
    }

    /// <summary>
    /// Asynchronously gets a vector by its logical index.
    /// </summary>
    public async ValueTask<Vector?> GetVectorAsync(long index, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (index < 0L || index >= Interlocked.Read(ref _count))
        {
            return null;
        }

        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryFindVectorByIndex(index, out var result))
                return null;

            return await ReadVectorAtLocationAsync((result.IndexPosition, result.DataOffset, result.Length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously gets a vector by its ID.
    /// </summary>
    public async ValueTask<Vector?> GetVectorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryFindVectorById(id, out var result))
                return null;

            return await ReadVectorAtLocationAsync((result.IndexPosition, result.DataOffset, result.Length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    public void CopyTo(Vector[] array, int arrayIndex)
    {
        using (_lock.EnterScope())
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array), "Array cannot be null");
            }

            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Array index cannot be less than 0");
            }

            if (array.Length - arrayIndex < Count)
            {
                throw new ArgumentException("The number of elements in the list is greater than the available space from arrayIndex to the end of the destination array");
            }

            // Use internal enumeration to avoid lock recursion
            CopyToInternal(array, arrayIndex);
        }
    }

    private void CopyToInternal(Vector[] array, int arrayIndex)
    {
        long maxIndexPos = _nextIndexPosition;
        long position = 0;

        while (position < maxIndexPos)
        {
            var entry = ReadIndexEntryAt(position);

            if (entry.Id.Equals(Guid.Empty))
                break;

            if (!entry.Id.Equals(s_tombStone))
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(entry.Length);
                try
                {
                    ReadDataIntoSpan(entry.DataOffset, buffer.AsSpan(0, entry.Length));
                    array[arrayIndex++] = new Vector(buffer.AsSpan(0, entry.Length));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            position += s_indexEntryByteLength;
        }
    }

    public long FindIndexById(Guid id)
    {
        using (_lock.EnterScope())
        {
            return TryFindVectorById(id, out var result) ? result.LogicalIndex : -1L;
        }
    }

    public long IndexOf(Vector item)
    {
        ArgumentNullException.ThrowIfNull(item);

        using (_lock.EnterScope())
        {
            return TryFindVectorById(item.Id, out var result) ? result.LogicalIndex : -1L;
        }
    }

    public void Add(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        using (_lock.EnterScope())
        {
            AddCore(vector, logToWAL: true);
        }
    }

    /// <summary>
    /// Asynchronously adds a vector to the list.
    /// </summary>
    public async ValueTask AddAsync(Vector vector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await AddCoreAsync(vector, logToWAL: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>
    /// Core add implementation. Caller must hold the lock.
    /// </summary>
    private void AddCore(Vector vector, bool logToWAL)
    {
        long indexPosition = _nextIndexPosition;
        long dataPosition = _nextDataPosition;
        byte[] data = vector.ToBinary();

        // CRITICAL FIX: Ensure capacity BEFORE any writes
        EnsureCapacity(s_indexEntryByteLength, data.Length);

        long lsn = 0;
        if (logToWAL)
        {
            // Step 1: Log to WAL first (WAL is fsynced in LogOperation)
            lsn = _wal.LogOperation(WALOperationType.Add, vector.Id, data, indexPosition, dataPosition);
        }

        // Step 2: Write to data files (in OS buffer)
        WriteVectorData(dataPosition, data);
        WriteIndexEntry(indexPosition, vector.Id, dataPosition, data.Length);

        _nextIndexPosition += s_indexEntryByteLength;
        _nextDataPosition += data.Length;

        if (logToWAL)
        {
            // Step 3: Record operation with LSN (may trigger checkpoint based on policy)
            // CheckpointManager handles WAL truncation after confirmed data flush
            _checkpointManager.RecordOperation(lsn);
        }

        Interlocked.Increment(ref _count);
    }

    /// <summary>
    /// Async core add implementation. Caller must hold the async lock.
    /// </summary>
    private async ValueTask AddCoreAsync(Vector vector, bool logToWAL, CancellationToken cancellationToken)
    {
        long indexPosition = _nextIndexPosition;
        long dataPosition = _nextDataPosition;
        byte[] data = vector.ToBinary();

        // CRITICAL FIX: Ensure capacity BEFORE any writes
        await EnsureCapacityAsync(s_indexEntryByteLength, data.Length, cancellationToken).ConfigureAwait(false);

        long lsn = 0;
        if (logToWAL)
        {
            // Step 1: Log to WAL first (WAL is fsynced in LogOperationAsync)
            lsn = await _wal.LogOperationAsync(WALOperationType.Add, vector.Id, data, indexPosition, dataPosition, cancellationToken).ConfigureAwait(false);
        }

        // Step 2: Write to data files (async I/O)
        await WriteVectorDataAsync(dataPosition, data, cancellationToken).ConfigureAwait(false);
        await WriteIndexEntryAsync(indexPosition, vector.Id, dataPosition, data.Length, cancellationToken).ConfigureAwait(false);

        _nextIndexPosition += s_indexEntryByteLength;
        _nextDataPosition += data.Length;

        if (logToWAL)
        {
            // Step 3: Record operation with LSN (may trigger checkpoint based on policy)
            // CheckpointManager handles WAL truncation after confirmed data flush
            await _checkpointManager.RecordOperationAsync(lsn, cancellationToken).ConfigureAwait(false);
        }

        Interlocked.Increment(ref _count);
    }

    public bool Remove(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        using (_lock.EnterScope())
        {
            if (!TryFindVectorById(vector.Id, out var result))
                return false;

            // Step 1: Log operation to WAL (WAL is fsynced in LogOperation)
            long lsn = _wal.LogOperation(WALOperationType.Remove, vector.Id, s_tombStoneBytes, result.IndexPosition, -1);

            // Step 2: Mark as tombstone in data file
            WriteTombstone(result.IndexPosition);

            // Step 3: Record operation (may trigger checkpoint based on policy)
            _checkpointManager.RecordOperation(lsn);

            // Decrement count atomically
            Interlocked.Decrement(ref _count);
            return true;
        }
    }

    /// <summary>
    /// Asynchronously removes a vector from the list.
    /// </summary>
    public async ValueTask<bool> RemoveAsync(Vector vector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryFindVectorById(vector.Id, out var result))
                return false;

            // Step 1: Log operation to WAL (WAL is fsynced in LogOperationAsync)
            long lsn = await _wal.LogOperationAsync(WALOperationType.Remove, vector.Id, s_tombStoneBytes, result.IndexPosition, -1, cancellationToken).ConfigureAwait(false);

            // Step 2: Mark as tombstone in data file
            await WriteTombstoneAsync(result.IndexPosition, cancellationToken).ConfigureAwait(false);

            // Step 3: Record operation (may trigger checkpoint based on policy)
            await _checkpointManager.RecordOperationAsync(lsn, cancellationToken).ConfigureAwait(false);

            // Decrement count atomically
            Interlocked.Decrement(ref _count);
            return true;
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    public bool Update(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        using (_lock.EnterScope())
        {
            // Find the vector using unified search method
            if (!TryFindVectorById(vector.Id, out var result))
                return false;

            byte[] newData = vector.ToBinary();
            long newDataPosition;

            if (newData.Length <= result.Length)
            {
                // Reuse existing space
                newDataPosition = result.DataOffset;

                // FIXED: Still check for threshold-triggered growth even when reusing space
                EnsureCapacity(0, 0);
            }
            else
            {
                // Need more space - append at end of data
                newDataPosition = _nextDataPosition;

                // FIXED: Removed inadequate capacity check (lines 491-493)
                // Old code: if (newDataPosition + newData.Length > _dataFile.Capacity) return false;
                // New code: Ensure capacity (will grow if needed)
                EnsureCapacity(0, newData.Length);

                // Update data position
                _nextDataPosition += newData.Length;
            }

            // Step 1: Log operation to WAL (WAL is fsynced in LogOperation)
            long lsn = _wal.LogOperation(WALOperationType.Update, vector.Id, newData, result.IndexPosition, newDataPosition);

            // Step 2: Write new data
            WriteVectorData(newDataPosition, newData);

            // Step 3: Update index entry with new offset and length
            UpdateIndexEntry(result.IndexPosition, newDataPosition, newData.Length);

            // Step 4: Record operation (may trigger checkpoint based on policy)
            _checkpointManager.RecordOperation(lsn);

            return true;
        }
    }

    /// <summary>
    /// Asynchronously updates a vector in the list.
    /// </summary>
    public async ValueTask<bool> UpdateAsync(Vector vector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Find the vector using unified search method
            if (!TryFindVectorById(vector.Id, out var result))
                return false;

            byte[] newData = vector.ToBinary();
            long newDataPosition;

            if (newData.Length <= result.Length)
            {
                // Reuse existing space
                newDataPosition = result.DataOffset;

                // FIXED: Still check for threshold-triggered growth even when reusing space
                await EnsureCapacityAsync(0, 0, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Need more space - append at end of data
                newDataPosition = _nextDataPosition;

                // FIXED: Removed inadequate capacity check (lines 545-547)
                // Old code: if (newDataPosition + newData.Length > _dataFile.Capacity) return false;
                // New code: Ensure capacity (will grow if needed)
                await EnsureCapacityAsync(0, newData.Length, cancellationToken).ConfigureAwait(false);

                // Update data position
                _nextDataPosition += newData.Length;
            }

            // Step 1: Log operation to WAL (WAL is fsynced in LogOperationAsync)
            long lsn = await _wal.LogOperationAsync(WALOperationType.Update, vector.Id, newData, result.IndexPosition, newDataPosition, cancellationToken).ConfigureAwait(false);

            // Step 2: Write new data
            await WriteVectorDataAsync(newDataPosition, newData, cancellationToken).ConfigureAwait(false);

            // Step 3: Update index entry with new offset and length
            await UpdateIndexEntryAsync(result.IndexPosition, newDataPosition, newData.Length, cancellationToken).ConfigureAwait(false);

            // Step 4: Record operation (may trigger checkpoint based on policy)
            await _checkpointManager.RecordOperationAsync(lsn, cancellationToken).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>
    /// Calculate the fragmentation of the data file
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public long CalculateFragmentation()
    {
        using (_lock.EnterScope())
        {
            long expectedDataPosition = 0;
            long totalFragmentation = 0;
            long totalDataSize = 0;

            long maxIndexPos = _nextIndexPosition;
            long position = 0;

            while (position < maxIndexPos)
            {
                var entry = ReadIndexEntryAt(position);

                if (entry.Id.Equals(Guid.Empty))
                    break;

                if (!entry.Id.Equals(s_tombStone))
                {
                    if (entry.DataOffset > expectedDataPosition)
                    {
                        totalFragmentation += entry.DataOffset - expectedDataPosition;
                    }

                    expectedDataPosition = entry.DataOffset + entry.Length;
                    totalDataSize += entry.Length;
                }

                position += s_indexEntryByteLength;
            }

            if (totalDataSize == 0)
                return 0;

            return totalFragmentation * 100 / totalDataSize;
        }
    }

    /// <summary>
    /// Performs a blocking defragmentation of the data file, regardless of the fragmentation level
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Defrag()
    {
        using (_lock.EnterScope())
        {
            long newIndexPosition = 0;
            long newDataPosition = 0;
            long maxIndexPos = _nextIndexPosition;
            long readPosition = 0;

            while (readPosition < maxIndexPos)
            {
                var entry = ReadIndexEntryAt(readPosition);

                if (entry.Id.Equals(Guid.Empty))
                    break;

                if (!entry.Id.Equals(s_tombStone))
                {
                    // Read data from old position using pooled buffer
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(entry.Length);
                    try
                    {
                        ReadDataIntoSpan(entry.DataOffset, buffer.AsSpan(0, entry.Length));

                        // Write data to new position
                        _dataFile.Write(newDataPosition, buffer.AsSpan(0, entry.Length));

                        // Write updated index entry at new position
                        WriteIndexEntry(newIndexPosition * s_indexEntryByteLength, entry.Id, newDataPosition, entry.Length);

                        newIndexPosition++;
                        newDataPosition += entry.Length;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                readPosition += s_indexEntryByteLength;
            }

            // Update positions after defrag
            _nextIndexPosition = newIndexPosition * s_indexEntryByteLength;
            _nextDataPosition = newDataPosition;
        }
    }

    /// <summary>
    /// Determines if defragmentation should be performed based on SSD optimization
    /// </summary>
    public bool ShouldDefragment()
    {
        long fragmentation = CalculateFragmentation();
        long totalDataSize = GetTotalDataSize();

        // Only defragment if fragmentation is high (>75%) and data size is significant
        return totalDataSize >= 128 * 1024 && fragmentation > 75;
    }

    /// <summary>
    /// Defragments the data file in batches, to avoid blocking I/O for long periods
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public long DefragBatch()
    {
        using (_lock.EnterScope())
        {
            long newDataPosition = _newDataPosition;
            long totalDataSize = 0;
            long totalFragmentation = 0;
            int entriesProcessed = 0;

            long maxIndexPos = _nextIndexPosition;
            List<(Guid id, long oldOffset, int length, long newOffset)> updates = new List<(Guid, long, int, long)>(_defragBatchSize);

            // Find the maximum entry size in this batch
            int maxEntrySize = 0;

            long currentReadPosition = _defragIndexPosition;
            while (entriesProcessed < _defragBatchSize && currentReadPosition * s_indexEntryByteLength < maxIndexPos)
            {
                var entry = ReadIndexEntryAt(currentReadPosition * s_indexEntryByteLength);

                if (entry.Id.Equals(s_tombStone))
                {
                    currentReadPosition++;
                    continue; // Skip tombstoned entries but don't count them
                }

                if (entry.Id.Equals(Guid.Empty))
                {
                    break; // End of valid entries
                }

                if (entry.DataOffset > newDataPosition)
                {
                    totalFragmentation += entry.DataOffset - newDataPosition;
                }

                updates.Add((entry.Id, entry.DataOffset, entry.Length, newDataPosition));
                maxEntrySize = Math.Max(maxEntrySize, entry.Length);

                newDataPosition += entry.Length;
                totalDataSize += entry.Length;
                entriesProcessed++;
                currentReadPosition++;
            }

            // Rent a buffer from the ArrayPool
            if (maxEntrySize > 0)
            {
                byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(maxEntrySize);

                try
                {
                    // Perform all reads and writes
                    foreach (var update in updates)
                    {
                        // Read data from old position
                        _dataFile.ReadExactly(update.oldOffset, sharedBuffer.AsSpan(0, update.length));

                        // Write data to new position
                        _dataFile.Write(update.newOffset, sharedBuffer.AsSpan(0, update.length));

                        // Write updated index entry at defrag position
                        WriteIndexEntry(_defragIndexPosition * s_indexEntryByteLength, update.id, update.newOffset, update.length);
                        _defragIndexPosition++;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(sharedBuffer);
                }
            }

            // Update tracking variables for the next batch
            _newDataPosition = newDataPosition;

            // Detecting the end of defragmentation
            if (entriesProcessed == 0 || currentReadPosition * s_indexEntryByteLength >= maxIndexPos)
            {
                // Reset state variables for the next defragmentation cycle
                _defragIndexPosition = 0;
                _newDataPosition = 0;
                return 0; // Defragmentation complete
            }

            // Calculate and return fragmentation percentage
            return totalDataSize == 0 ? 0 : totalFragmentation * 100 / totalDataSize;
        }
    }

    public void Clear()
    {
        using (_lock.EnterScope())
        {
            _indexFile.DisposeHandle();
            _indexFile.Reset();
            _dataFile.DisposeHandle();
            _dataFile.Reset();
            Interlocked.Exchange(ref _count, 0);
            _nextIndexPosition = 0;
            _nextDataPosition = 0;
        }
    }

    public bool Contains(Vector item)
    {
        if (item is null)
        {
            return false;
        }

        using (_lock.EnterScope())
        {
            // Check for existence by ID only, not value equality
            long maxIndexPos = _nextIndexPosition;
            long position = 0;

            while (position < maxIndexPos)
            {
                var entry = ReadIndexEntryAt(position);

                if (item.Id == entry.Id)
                {
                    return true;
                }
                else if (entry.Id.Equals(Guid.Empty))
                {
                    // Hit the end of actual entries
                    break;
                }

                position += s_indexEntryByteLength;
            }

            return false;
        }
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        ThrowIfDisposed();

        // Snapshot approach - read all valid locations under lock
        List<(long offset, int length)> locations;
        using (_lock.EnterScope())
        {
            locations = GetAllValidLocations();
        }

        // Yield vectors without holding lock
        foreach (var (offset, length) in locations)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                ReadDataIntoSpan(offset, buffer.AsSpan(0, length));
                yield return new Vector(buffer.AsSpan(0, length));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Asynchronously enumerates all vectors in the list.
    /// </summary>
    public async IAsyncEnumerator<Vector> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Snapshot approach - read all valid locations under lock
        List<(long offset, int length)> locations;
        await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            locations = GetAllValidLocations();
        }
        finally
        {
            _asyncLock.Release();
        }

        // Yield vectors without holding lock
        foreach (var (offset, length) in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                await ReadDataIntoMemoryAsync(offset, buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                yield return new Vector(buffer.AsSpan(0, length));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    /// <summary>
    /// Unified vector search result containing both logical index and physical location.
    /// </summary>
    private readonly record struct VectorSearchResult(
        long LogicalIndex,
        long IndexPosition,
        long DataOffset,
        int Length);

    /// <summary>
    /// Core search method that returns both logical index and physical location.
    /// </summary>
    private bool TryFindVectorById(Guid id, out VectorSearchResult result)
    {
        long position = 0;
        long logicalIndex = 0L;

        while (position < _nextIndexPosition)
        {
            var entry = ReadIndexEntryAt(position);

            if (entry.Id == id)
            {
                result = new VectorSearchResult(logicalIndex, position, entry.DataOffset, entry.Length);
                return true;
            }

            if (entry.Id.Equals(Guid.Empty))
                break;

            if (!entry.Id.Equals(s_tombStone))
                logicalIndex++;

            position += s_indexEntryByteLength;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Returns the actual disk space used by the Index and Data files.
    /// </summary>
    /// <returns>
    /// [0] = bytes allocated for Index file
    /// [1] = total (sparce) capacity of Index file
    /// [2] = bytes allocated for Data file
    /// [3] = total (sparce) capacity of Data file
    /// </returns>
    /// <seealso cref="Flush"/>
    internal long[] GetFileInfo()
    {
        return MemoryMappedFileServices.GetFileInfo(_indexFile, _dataFile);
    }

    internal void ReleaseMappedMemory()
    {
        using (_lock.EnterScope())
        {
            // Dispose and recreate file handles to release memory
            // This allows the OS to reclaim resources under memory pressure
            _indexFile.DisposeHandle();
            _dataFile.DisposeHandle();

            // Recreate the file handles
            _indexFile.Reset();
            _dataFile.Reset();

            // Reinitialize positions after reset
            _nextIndexPosition = 0;
            _nextDataPosition = 0;
            InitializeAppendMetadataInternal();
        }
    }

    /// <summary>
    /// Scans the index file to initialize append positions and count.
    /// Caller must hold the lock.
    /// </summary>
    private void InitializeAppendMetadataInternal()
    {
        long maxIndexPos = 0;
        long maxDataPos = 0;
        long fileLength = _indexFile.GetLength();
        long count = 0;

        for (long pos = 0; pos < fileLength; pos += s_indexEntryByteLength)
        {
            var entry = ReadIndexEntryAt(pos);

            if (entry.Id.Equals(Guid.Empty))
            {
                maxIndexPos = pos;
                break;
            }

            if (!entry.Id.Equals(s_tombStone))
            {
                var endOfData = entry.DataOffset + entry.Length;
                if (endOfData > maxDataPos)
                    maxDataPos = endOfData;
                count++;
            }
        }

        _nextIndexPosition = maxIndexPos;
        _nextDataPosition = maxDataPos;
        Interlocked.Exchange(ref _count, count);
    }

    private void RecoverFromWAL()
    {
        using (_lock.EnterScope())
        {
            var entries = _wal.ReadEntries();
            if (entries.Count == 0)
                return;

            Logging.Logger.Information("Recovering {EntryCount} operations from WAL", entries.Count);

            // Sort by LSN to ensure correct replay order
            var sortedEntries = entries.OrderBy(e => e.LSN).ToList();
            long lastRecoveredLSN = 0;

            foreach (var entry in sortedEntries)
            {
                try
                {
                    switch (entry.Operation)
                    {
                        case WALOperationType.Add:
                            RecoverAdd(entry);
                            break;
                        case WALOperationType.Remove:
                            RecoverRemove(entry);
                            break;
                        case WALOperationType.Update:
                            RecoverUpdate(entry);
                            break;
                    }
                    lastRecoveredLSN = entry.LSN;
                }
                catch (Exception ex)
                {
                    Logging.Logger.Error(ex, "Failed to recover WAL entry for vector {VectorId} (LSN: {LSN})",
                        entry.VectorId, entry.LSN);
                }
            }

            // After successful recovery, checkpoint to truncate recovered entries
            if (lastRecoveredLSN > 0)
            {
                // Flush data files first, then checkpoint WAL
                _indexFile.FlushToDisk();
                _dataFile.FlushToDisk();
                _wal.Checkpoint(lastRecoveredLSN);
            }

            Logging.Logger.Information("WAL recovery completed");
        }
    }

    private void RecoverAdd(WALEntry entry)
    {
        if (entry.VectorData == null)
        {
            Logging.Logger.Warning("Skipping Add recovery for {VectorId}: no vector data", entry.VectorId);
            return;
        }

        // Check if vector already exists (idempotency)
        if (TryFindVectorById(entry.VectorId, out _))
        {
            Logging.Logger.Debug("Vector {VectorId} already exists, skipping Add recovery", entry.VectorId);
            return;
        }

        var vector = new Vector(entry.VectorData);

        // Use the recorded positions if valid, otherwise append
        long indexPosition = entry.IndexPosition >= 0 ? entry.IndexPosition : _nextIndexPosition;
        long dataPosition = entry.DataPosition >= 0 ? entry.DataPosition : _nextDataPosition;
        byte[] data = vector.ToBinary();

        // FIXED: Ensure capacity during recovery
        long indexSpaceNeeded = indexPosition >= _nextIndexPosition ? s_indexEntryByteLength : 0;
        long dataSpaceNeeded = dataPosition >= _nextDataPosition ? data.Length : 0;
        EnsureCapacity(indexSpaceNeeded, dataSpaceNeeded);

        WriteVectorData(dataPosition, data);
        WriteIndexEntry(indexPosition, vector.Id, dataPosition, data.Length);

        // Update positions if we used new positions
        if (indexPosition >= _nextIndexPosition)
            _nextIndexPosition = indexPosition + s_indexEntryByteLength;
        if (dataPosition >= _nextDataPosition)
            _nextDataPosition = dataPosition + data.Length;

        Interlocked.Increment(ref _count);

        Logging.Logger.Debug("Recovered Add for vector {VectorId}", entry.VectorId);
    }

    private void RecoverRemove(WALEntry entry)
    {
        // Find the vector by ID
        if (!TryFindVectorById(entry.VectorId, out var result))
        {
            Logging.Logger.Debug("Vector {VectorId} not found for Remove recovery (already removed?)", entry.VectorId);
            return;
        }

        // Check if already tombstoned
        var currentEntry = ReadIndexEntryAt(result.IndexPosition);
        if (currentEntry.Id.Equals(s_tombStone))
        {
            Logging.Logger.Debug("Vector {VectorId} already tombstoned, skipping Remove recovery", entry.VectorId);
            return;
        }

        // Apply the tombstone
        WriteTombstone(result.IndexPosition);
        Interlocked.Decrement(ref _count);

        Logging.Logger.Debug("Recovered Remove for vector {VectorId}", entry.VectorId);
    }

    private void RecoverUpdate(WALEntry entry)
    {
        if (entry.VectorData == null)
        {
            Logging.Logger.Warning("Skipping Update recovery for {VectorId}: no vector data", entry.VectorId);
            return;
        }

        // Find the vector by ID
        if (!TryFindVectorById(entry.VectorId, out var result))
        {
            Logging.Logger.Warning("Vector {VectorId} not found for Update recovery", entry.VectorId);
            return;
        }

        byte[] newData = entry.VectorData;
        long newDataPosition = entry.DataPosition;

        // Validate positions from WAL entry
        if (newDataPosition < 0)
        {
            // Invalid position in WAL, use current append position
            newDataPosition = _nextDataPosition;
        }

        // Write new data
        WriteVectorData(newDataPosition, newData);

        // Update index entry with new offset and length
        UpdateIndexEntry(result.IndexPosition, newDataPosition, newData.Length);

        // Update data position if needed
        if (newDataPosition + newData.Length > _nextDataPosition)
        {
            _nextDataPosition = newDataPosition + newData.Length;
        }

        Logging.Logger.Debug("Recovered Update for vector {VectorId}", entry.VectorId);
    }

    private void ValidateFileIntegrity()
    {
        using (_lock.EnterScope())
        {
            // Quick validation for empty files
            if (_indexFile.GetLength() <= 0 &&
                _dataFile.GetLength() <= 0)
            {
                return; // New empty files are valid
            }

            bool indexValid = CorruptionDetector.ValidateIndexFile(_indexFile, _count);
            bool dataValid = CorruptionDetector.ValidateDataFile(_dataFile);

            if (!indexValid || !dataValid)
            {
                Logging.Logger.Warning("File corruption detected. Index valid: {IndexValid}, Data valid: {DataValid}",
                    indexValid, dataValid);

                CorruptionDetector.AttemptRepair(_indexFile, _dataFile);

                // Recalculate count after repair
                RecalculateCountInternal();

                Logging.Logger.Information("File repair completed. New count: {Count}", _count);
            }
        }
    }

    private void RecalculateCount()
    {
        using (_lock.EnterScope())
        {
            RecalculateCountInternal();
        }
    }

    private void RecalculateCountInternal()
    {
        // Internal version that doesn't acquire lock (caller must hold lock)
        long count = 0;
        try
        {
            long fileLength = _indexFile.GetLength();
            long position = 0;

            while (position + s_indexEntryByteLength <= fileLength)
            {
                var entry = ReadIndexEntryAt(position);

                if (entry.Id.Equals(Guid.Empty))
                    break;

                if (!entry.Id.Equals(s_tombStone))
                {
                    count++;
                }

                position += s_indexEntryByteLength;
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Failed to recalculate count, using 0");
        }

        Interlocked.Exchange(ref _count, count);
    }

    private long GetTotalDataSize()
    {
        return _nextDataPosition;
    }

    // Helper methods for position-independent operations

    /// <summary>
    /// Finds a vector by its logical index position.
    /// </summary>
    private bool TryFindVectorByIndex(long targetIndex, out VectorSearchResult result)
    {
        long currentIndex = 0;

        for (long pos = 0; pos < _nextIndexPosition; pos += s_indexEntryByteLength)
        {
            var entry = ReadIndexEntryAt(pos);

            if (entry.Id.Equals(Guid.Empty))
                break;

            if (!entry.Id.Equals(s_tombStone))
            {
                if (currentIndex == targetIndex)
                {
                    result = new VectorSearchResult(currentIndex, pos, entry.DataOffset, entry.Length);
                    return true;
                }
                currentIndex++;
            }
        }

        result = default;
        return false;
    }
    
    private Vector ReadVectorAtLocation((long IndexPosition, long DataOffset, int Length) location)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(location.Length);
        try
        {
            ReadDataIntoSpan(location.DataOffset, buffer.AsSpan(0, location.Length));
            return new Vector(buffer.AsSpan(0, location.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async ValueTask<Vector> ReadVectorAtLocationAsync((long IndexPosition, long DataOffset, int Length) location, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(location.Length);
        try
        {
            await ReadDataIntoMemoryAsync(location.DataOffset, buffer.AsMemory(0, location.Length), cancellationToken).ConfigureAwait(false);
            return new Vector(buffer.AsSpan(0, location.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ReadDataIntoSpan(long offset, Span<byte> buffer)
    {
        _dataFile.ReadExactly(offset, buffer);
    }

    private ValueTask ReadDataIntoMemoryAsync(long offset, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _dataFile.ReadExactlyAsync(offset, buffer, cancellationToken);
    }

    private (Guid Id, long DataOffset, int Length) ReadIndexEntryAt(long position)
    {
        Span<byte> buffer = stackalloc byte[s_indexEntryByteLength];
        _indexFile.ReadExactly(position, buffer);

        var id = new Guid(buffer[..s_idBytesLength]);
        var dataOffset = BitConverter.ToInt64(buffer.Slice(s_idBytesLength, s_offsetBytesLength));
        var length = BitConverter.ToInt32(buffer.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));

        return (id, dataOffset, length);
    }

    private void WriteVectorData(long position, byte[] data)
    {
        // RandomAccess is thread-safe for non-overlapping writes
        _dataFile.Write(position, data);
    }

    private ValueTask WriteVectorDataAsync(long position, byte[] data, CancellationToken cancellationToken)
    {
        return _dataFile.WriteAsync(position, data, cancellationToken);
    }

    private void WriteIndexEntry(long position, Guid id, long dataOffset, int dataLength)
    {
        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];

        if (!id.TryWriteBytes(entry[..s_idBytesLength]))
            throw new InvalidOperationException("Failed to write ID to bytes");

        if (!BitConverter.TryWriteBytes(entry[s_idBytesLength..], dataOffset))
            throw new InvalidOperationException("Failed to write offset to bytes");

        if (!BitConverter.TryWriteBytes(entry[(s_idBytesLength + s_offsetBytesLength)..], dataLength))
            throw new InvalidOperationException("Failed to write length to bytes");

        _indexFile.Write(position, entry);
    }

    private async ValueTask WriteIndexEntryAsync(long position, Guid id, long dataOffset, int dataLength, CancellationToken cancellationToken)
    {
        byte[] entry = ArrayPool<byte>.Shared.Rent(s_indexEntryByteLength);
        try
        {
            if (!id.TryWriteBytes(entry.AsSpan(0, s_idBytesLength)))
                throw new InvalidOperationException("Failed to write ID to bytes");

            if (!BitConverter.TryWriteBytes(entry.AsSpan(s_idBytesLength), dataOffset))
                throw new InvalidOperationException("Failed to write offset to bytes");

            if (!BitConverter.TryWriteBytes(entry.AsSpan(s_idBytesLength + s_offsetBytesLength), dataLength))
                throw new InvalidOperationException("Failed to write length to bytes");

            await _indexFile.WriteAsync(position, entry.AsMemory(0, s_indexEntryByteLength), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(entry);
        }
    }

    private void WriteTombstone(long indexPosition)
    {
        _indexFile.Write(indexPosition, s_tombStoneBytes);
    }

    private ValueTask WriteTombstoneAsync(long indexPosition, CancellationToken cancellationToken)
    {
        return _indexFile.WriteAsync(indexPosition, s_tombStoneBytes, cancellationToken);
    }

    private void UpdateIndexEntry(long position, long newDataOffset, int newDataLength)
    {
        Span<byte> buffer = stackalloc byte[s_offsetBytesLength + s_lengthBytesLength];

        if (!BitConverter.TryWriteBytes(buffer[..s_offsetBytesLength], newDataOffset))
            throw new InvalidOperationException("Failed to write offset to bytes");

        if (!BitConverter.TryWriteBytes(buffer[s_offsetBytesLength..], newDataLength))
            throw new InvalidOperationException("Failed to write length to bytes");

        _indexFile.Write(position + s_idBytesLength, buffer);
    }

    private async ValueTask UpdateIndexEntryAsync(long position, long newDataOffset, int newDataLength, CancellationToken cancellationToken)
    {
        const int updateBufferSize = s_offsetBytesLength + s_lengthBytesLength;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(updateBufferSize);
        try
        {
            if (!BitConverter.TryWriteBytes(buffer.AsSpan(0, s_offsetBytesLength), newDataOffset))
                throw new InvalidOperationException("Failed to write offset to bytes");

            if (!BitConverter.TryWriteBytes(buffer.AsSpan(s_offsetBytesLength), newDataLength))
                throw new InvalidOperationException("Failed to write length to bytes");

            await _indexFile.WriteAsync(position + s_idBytesLength, buffer.AsMemory(0, updateBufferSize), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    private List<(long offset, int length)> GetAllValidLocations()
    {
        var locations = new List<(long, int)>();
        long maxIndexPos = _nextIndexPosition;

        for (long pos = 0; pos < maxIndexPos; pos += s_indexEntryByteLength)
        {
            var entry = ReadIndexEntryAt(pos);

            if (entry.Id.Equals(Guid.Empty))
                break;

            if (!entry.Id.Equals(s_tombStone))
            {
                locations.Add((entry.DataOffset, entry.Length));
            }
        }

        return locations;
    }

    private void InitializeAppendMetadata()
    {
        using (_lock.EnterScope())
        {
            InitializeAppendMetadataInternal();
        }
    }
    
    /// <summary>
    /// Cleans up orphaned temporary files from crashed resize operations.
    /// Called during constructor before recovery.
    /// </summary>
    private void CleanupOrphanedTempFiles()
    {
        try
        {
            // Find temp files in same directory as our files
            string directory = Path.GetDirectoryName(_indexFile.Filename) ?? Path.GetTempPath();
            if (!Directory.Exists(directory))
                return;

            var tempFiles = Directory.GetFiles(directory, "*.tmp");

            foreach (var tempFile in tempFiles)
            {
                try
                {
                    // Check if temp file is old (within last hour)
                    // to avoid deleting active temp files from other processes
                    var fileInfo = new FileInfo(tempFile);
                    if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromHours(1))
                    {
                        File.Delete(tempFile);
                        Logging.Logger.Information("Deleted orphaned temp file: {TempFile}", tempFile);
                    }
                }
                catch (Exception ex)
                {
                    Logging.Logger.Warning(ex, "Failed to delete temp file: {TempFile}", tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Failed to cleanup orphaned temp files");
        }
    }

    /// <summary>
    /// Ensures sufficient capacity for upcoming write operation.
    /// Grows files if needed based on growth strategy.
    /// Caller MUST hold _lock.
    /// </summary>
    /// <param name="indexSpaceNeeded">Additional index space required (bytes).</param>
    /// <param name="dataSpaceNeeded">Additional data space required (bytes).</param>
    private void EnsureCapacity(long indexSpaceNeeded, long dataSpaceNeeded)
    {
        // Check and grow index file if needed
        long? newIndexCapacity = _indexFile.CheckAndCalculateGrowth(_nextIndexPosition + indexSpaceNeeded);
        if (newIndexCapacity.HasValue)
        {
            Logging.Logger.Information(
                "Growing index file from {CurrentCapacity} to {NewCapacity} bytes (usage: {Usage} bytes)",
                _indexFile.Capacity, newIndexCapacity.Value, _nextIndexPosition);

            _indexFile.ResizeFile(newIndexCapacity.Value);
        }

        // Check and grow data file if needed
        long? newDataCapacity = _dataFile.CheckAndCalculateGrowth(_nextDataPosition + dataSpaceNeeded);
        if (newDataCapacity.HasValue)
        {
            Logging.Logger.Information(
                "Growing data file from {CurrentCapacity} to {NewCapacity} bytes (usage: {Usage} bytes)",
                _dataFile.Capacity, newDataCapacity.Value, _nextDataPosition);

            _dataFile.ResizeFile(newDataCapacity.Value);
        }
    }

    /// <summary>
    /// Async version of EnsureCapacity for async code paths.
    /// Caller MUST hold _asyncLock.
    /// </summary>
    private async ValueTask EnsureCapacityAsync(
        long indexSpaceNeeded,
        long dataSpaceNeeded,
        CancellationToken cancellationToken)
    {
        // Check and grow index file if needed
        long? newIndexCapacity = _indexFile.CheckAndCalculateGrowth(_nextIndexPosition + indexSpaceNeeded);
        if (newIndexCapacity.HasValue)
        {
            Logging.Logger.Information(
                "Growing index file from {CurrentCapacity} to {NewCapacity} bytes (usage: {Usage} bytes)",
                _indexFile.Capacity, newIndexCapacity.Value, _nextIndexPosition);

            await _indexFile.ResizeFileAsync(newIndexCapacity.Value, cancellationToken).ConfigureAwait(false);
        }

        // Check and grow data file if needed
        long? newDataCapacity = _dataFile.CheckAndCalculateGrowth(_nextDataPosition + dataSpaceNeeded);
        if (newDataCapacity.HasValue)
        {
            Logging.Logger.Information(
                "Growing data file from {CurrentCapacity} to {NewCapacity} bytes (usage: {Usage} bytes)",
                _dataFile.Capacity, newDataCapacity.Value, _nextDataPosition);

            await _dataFile.ResizeFileAsync(newDataCapacity.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposedValue)
            throw new ObjectDisposedException(nameof(MemoryMappedList));
    }
}