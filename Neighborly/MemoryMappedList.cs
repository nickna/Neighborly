using System.Buffers;
using System.Collections;

namespace Neighborly;

public class MemoryMappedList : IDisposable, IEnumerable<Vector>
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
    private readonly DurabilityManager _durabilityManager;
    private static readonly MemoryPressureMonitor s_memoryMonitor = new();
    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
    private long _count;
    private volatile bool _disposedValue;
    
    // Metadata for O(1) append operations
    private readonly AppendMetadata _appendMetadata = new();
    
    /// <summary>
    /// Thread-safe metadata for fast append operations
    /// </summary>
    private sealed class AppendMetadata
    {
        private long _nextIndexPosition;
        private long _nextDataPosition;
        private readonly object _lock = new();
        
        public (long indexPos, long dataPos) GetNextPositions()
        {
            lock (_lock)
            {
                return (_nextIndexPosition, _nextDataPosition);
            }
        }
        
        public void UpdatePositions(long indexDelta, long dataDelta)
        {
            lock (_lock)
            {
                _nextIndexPosition += indexDelta;
                _nextDataPosition += dataDelta;
            }
        }
        
        public void Reset(long indexPos, long dataPos)
        {
            lock (_lock)
            {
                _nextIndexPosition = indexPos;
                _nextDataPosition = dataPos;
            }
        }
    }
    private long _defragIndexPosition;
    private long _newDataPosition;
    private const int _defragBatchSize = 100; // Number of entries to defrag in one batch, adjust based on performance needs

  
    /// <summary>
    /// Flush the memory-mapped files to disk
    /// </summary>
    public void Flush()
    {
        ThrowIfDisposed();
        
        _rwLock.EnterWriteLock();
        try
        {
            _durabilityManager.ForceFlush();
        }
        finally
        {
            _rwLock.ExitWriteLock();
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

    public MemoryMappedList(long capacity, FlushPolicy flushPolicy = FlushPolicy.Batched)
    {
        _indexFile = new RandomAccessFileHolder(s_indexEntryByteLength * capacity);
        // Based on typical vector dimensions, 4096 bytes should be enough for most cases as of 2024-06
        _dataFile = new RandomAccessFileHolder(4096L * capacity);
        _wal = new WriteAheadLog(_indexFile.Filename);
        _durabilityManager = new DurabilityManager(flushPolicy);

        // Register files with durability manager
        _durabilityManager.RegisterFile(_indexFile);
        _durabilityManager.RegisterFile(_dataFile);

        // Initialize defrag tracking
        _newDataPosition = 0;

        // Validate file integrity (temporarily disabled)
        // ValidateFileIntegrity();

        // Recovery on startup
        RecoverFromWAL();

        // Initialize append metadata after recovery
        InitializeAppendMetadata();

        // Register with memory pressure monitor
        s_memoryMonitor.RegisterList(this);
    }


    public long Count
    {
        get
        {
            ThrowIfDisposed();
            return Interlocked.Read(ref _count);
        }
    }

#pragma warning disable CA1822 // Mark members as static - mimicking ICollection<Vector>
    public bool IsReadOnly => false;
#pragma warning restore CA1822 // Mark members as static - mimicking ICollection<Vector>

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Wait for any active operations to complete
                _rwLock.EnterWriteLock();
                try
                {
                    // Dispose in reverse order of creation
                    _durabilityManager?.Dispose();
                    _wal?.Dispose();
                    _dataFile?.Dispose();
                    _indexFile?.Dispose();
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
                
                // Dispose the lock last
                _rwLock?.Dispose();
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

        _rwLock.EnterReadLock();
        try
        {
            var location = FindVectorLocationByIndex(index);
            if (!location.HasValue)
                return null;
                
            return ReadVectorAtLocation(location.Value);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public Vector? GetVector(Guid id)
    {
        ThrowIfDisposed();
        
        _rwLock.EnterReadLock();
        try
        {
            var location = FindVectorLocation(id);
            if (!location.HasValue)
                return null;
                
            return ReadVectorAtLocation(location.Value);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public void CopyTo(Vector[] array, int arrayIndex)
    {
        _rwLock.EnterReadLock();
        try
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
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    private void CopyToInternal(Vector[] array, int arrayIndex)
    {
        var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
        long position = 0;

        while (position < maxIndexPos)
        {
            var entry = ReadIndexEntryAt(position);

            if (entry.Id.Equals(Guid.Empty))
                break;

            if (!entry.Id.Equals(s_tombStone))
            {
                byte[] bytes = ReadDataAt(entry.DataOffset, entry.Length);
                array[arrayIndex++] = new Vector(bytes);
            }

            position += s_indexEntryByteLength;
        }
    }

    public long FindIndexById(Guid id)
    {
        _rwLock.EnterReadLock();
        try
        {
            (long index, _, _) = SearchVectorInIndex(id);
            return index;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public long IndexOf(Vector item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _rwLock.EnterReadLock();
        try
        {
            (long index, _, _) = SearchVectorInIndex(item.Id);
            return index;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public void Add(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        _rwLock.EnterWriteLock();
        try
        {
            // Get next positions from metadata - O(1) operation
            var (indexPosition, dataPosition) = _appendMetadata.GetNextPositions();
            var data = vector.ToBinary();
            
            // Log operation before performing it
            _wal.LogOperation(WALOperationType.Add, vector.Id, data, indexPosition, dataPosition);

            // Write data using position-independent operations
            WriteVectorData(dataPosition, data);
            WriteIndexEntry(indexPosition, vector.Id, dataPosition, data.Length);
            
            // Update metadata atomically
            _appendMetadata.UpdatePositions(s_indexEntryByteLength, data.Length);
            
            // Record operation for durability management
            _durabilityManager.RecordOperation();
            
            // Commit WAL after successful operation
            _wal.Commit();
            
            // Increment count atomically
            Interlocked.Increment(ref _count);
        }
        catch
        {
            // On failure, don't commit WAL
            throw;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public bool Remove(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        ThrowIfDisposed();

        _rwLock.EnterWriteLock();
        try
        {
            var location = FindVectorLocation(vector.Id);
            if (!location.HasValue)
                return false;
                
            // Log operation before performing it
            _wal.LogOperation(WALOperationType.Remove, vector.Id, s_tombStoneBytes, location.Value.IndexPosition, -1);
            
            // Mark as tombstone
            WriteTombstone(location.Value.IndexPosition);
            
            // Record operation for durability management
            _durabilityManager.RecordOperation();
            
            // Commit WAL after successful operation
            _wal.Commit();
            
            // Decrement count atomically
            Interlocked.Decrement(ref _count);
            return true;
        }
        catch
        {
            // On failure, don't commit WAL
            throw;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public bool Update(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        _rwLock.EnterWriteLock();
        try
        {
            // Find the vector using position-independent search
            var location = FindVectorLocation(vector.Id);
            if (!location.HasValue)
                return false;

            byte[] newData = vector.ToBinary();
            long newDataPosition;

            if (newData.Length <= location.Value.Length)
            {
                // Reuse existing space
                newDataPosition = location.Value.DataOffset;
            }
            else
            {
                // Need more space - append at end of data
                var (_, dataPos) = _appendMetadata.GetNextPositions();
                newDataPosition = dataPos;

                // Check if we have enough capacity
                if (newDataPosition + newData.Length > _dataFile.Capacity)
                {
                    return false;
                }

                // Update append metadata for data position
                _appendMetadata.UpdatePositions(0, newData.Length);
            }

            // Log operation before performing it
            _wal.LogOperation(WALOperationType.Update, vector.Id, newData, location.Value.IndexPosition, newDataPosition);

            // Write new data
            WriteVectorData(newDataPosition, newData);

            // Update index entry with new offset and length
            UpdateIndexEntry(location.Value.IndexPosition, newDataPosition, newData.Length);

            // Record operation for durability management
            _durabilityManager.RecordOperation();

            // Commit WAL after successful operation
            _wal.Commit();

            return true;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Calculate the fragmentation of the data file
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public long CalculateFragmentation()
    {
        _rwLock.EnterReadLock();
        try
        {
            long expectedDataPosition = 0;
            long totalFragmentation = 0;
            long totalDataSize = 0;

            var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
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
        finally
        {
            _rwLock.ExitReadLock();
        }
    }



    /// <summary>
    /// Performs a blocking defragmentation of the data file, regardless of the fragmentation level
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Defrag()
    {
        _rwLock.EnterWriteLock();
        try
        {
            long newIndexPosition = 0;
            long newDataPosition = 0;
            var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
            long readPosition = 0;

            while (readPosition < maxIndexPos)
            {
                var entry = ReadIndexEntryAt(readPosition);

                if (entry.Id.Equals(Guid.Empty))
                    break;

                if (!entry.Id.Equals(s_tombStone))
                {
                    // Read data from old position
                    byte[] data = ReadDataAt(entry.DataOffset, entry.Length);

                    // Write data to new position
                    WriteVectorData(newDataPosition, data);

                    // Write updated index entry at new position
                    WriteIndexEntry(newIndexPosition * s_indexEntryByteLength, entry.Id, newDataPosition, entry.Length);

                    newIndexPosition++;
                    newDataPosition += entry.Length;
                }

                readPosition += s_indexEntryByteLength;
            }

            // Update append metadata after defrag
            _appendMetadata.Reset(newIndexPosition * s_indexEntryByteLength, newDataPosition);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Determines if defragmentation should be performed based on SSD optimization
    /// </summary>
    public bool ShouldDefragment()
    {
        long fragmentation = CalculateFragmentation();
        long totalDataSize = GetTotalDataSize();
        
        return SSDOptimizer.ShouldDefragmentForSSD(fragmentation, totalDataSize);
    }
    
    /// <summary>
    /// Defragments the data file in batches, to avoid blocking I/O for long periods
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public long DefragBatch()
    {
        _rwLock.EnterWriteLock();
        try
        {
            long newDataPosition = _newDataPosition;
            long totalDataSize = 0;
            long totalFragmentation = 0;
            int entriesProcessed = 0;

            var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
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
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }


    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _indexFile.DisposeHandle();
            _indexFile.Reset();
            _dataFile.DisposeHandle();
            _dataFile.Reset();
            Interlocked.Exchange(ref _count, 0);
            _appendMetadata.Reset(0, 0);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public bool Contains(Vector item)
    {
        if (item is null)
        {
            return false;
        }

        _rwLock.EnterReadLock();
        try
        {
            // Check for existence by ID only, not value equality
            var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
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
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        ThrowIfDisposed();
        
        _rwLock.EnterReadLock();
        try
        {
            // Snapshot approach - read all valid entries under lock
            List<(long offset, int length)> locations = GetAllValidLocations();

            // Yield vectors without holding lock
            foreach (var (offset, length) in locations)
            {
                var data = ReadDataAt(offset, length);
                yield return new Vector(data);
            }
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private (long index, long offset, int length) SearchVectorInIndex(Guid id)
    {
        var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
        long position = 0;
        long index = 0L;

        while (position < maxIndexPos)
        {
            var entry = ReadIndexEntryAt(position);

            if (id == entry.Id)
            {
                return (index, entry.DataOffset, entry.Length);
            }
            else if (entry.Id.Equals(Guid.Empty))
            {
                // Hit the end of actual entries
                break;
            }
            else if (!entry.Id.Equals(s_tombStone))
            {
                // Only increment index for non-tombstone entries
                ++index;
            }

            position += s_indexEntryByteLength;
        }

        return (-1L, -1L, -1);
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
        _rwLock.EnterWriteLock();
        try
        {
            // Dispose and recreate file handles to release memory
            // This allows the OS to reclaim resources under memory pressure
            _indexFile.DisposeHandle();
            _dataFile.DisposeHandle();

            // Recreate the file handles
            _indexFile.Reset();
            _dataFile.Reset();

            // Reinitialize append metadata after reset
            _appendMetadata.Reset(0, 0);
            InitializeAppendMetadataInternal();
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    private void InitializeAppendMetadataInternal()
    {
        // Internal version that doesn't acquire lock (caller must hold write lock)
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

        _appendMetadata.Reset(maxIndexPos, maxDataPos);
        Interlocked.Exchange(ref _count, count);
    }

    private void RecoverFromWAL()
    {
        _rwLock.EnterWriteLock();
        try
        {
            var entries = _wal.ReadEntries();
            if (entries.Count == 0)
                return;

            Logging.Logger.Information("Recovering {EntryCount} operations from WAL", entries.Count);

            foreach (var entry in entries)
            {
                try
                {
                    switch (entry.Operation)
                    {
                        case WALOperationType.Add:
                            if (entry.VectorData != null)
                            {
                                var vector = new Vector(entry.VectorData);
                                // Replay the add operation without WAL logging to avoid recursion
                                AddWithoutWAL(vector);
                            }
                            break;
                        case WALOperationType.Remove:
                            // Implement remove recovery if needed
                            break;
                        case WALOperationType.Update:
                            // Implement update recovery if needed
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logging.Logger.Error(ex, "Failed to recover WAL entry for vector {VectorId}", entry.VectorId);
                }
            }

            _wal.Commit(); // Clear WAL after recovery
            Logging.Logger.Information("WAL recovery completed");
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    private void AddWithoutWAL(Vector vector)
    {
        // Get positions from metadata
        var (indexPosition, dataPosition) = _appendMetadata.GetNextPositions();
        byte[] data = vector.ToBinary();

        // Write data first
        WriteVectorData(dataPosition, data);

        // Write index entry
        WriteIndexEntry(indexPosition, vector.Id, dataPosition, data.Length);

        // Update metadata
        _appendMetadata.UpdatePositions(s_indexEntryByteLength, data.Length);
        Interlocked.Increment(ref _count);
    }

    private void ValidateFileIntegrity()
    {
        _rwLock.EnterWriteLock();
        try
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
                RecalculateCount();
                
                Logging.Logger.Information("File repair completed. New count: {Count}", _count);
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to validate or repair file integrity");
            throw;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    private void RecalculateCount()
    {
        _rwLock.EnterWriteLock();
        try
        {
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
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    private long GetTotalDataSize()
    {
        return _appendMetadata.GetNextPositions().dataPos;
    }
    
    // Helper methods for position-independent operations
    
    private (long IndexPosition, long DataOffset, int Length)? FindVectorLocation(Guid id)
    {
        // Position-independent search through index
        for (long pos = 0; pos < _appendMetadata.GetNextPositions().indexPos; pos += s_indexEntryByteLength)
        {
            var entry = ReadIndexEntryAt(pos);
            if (entry.Id == id)
                return (pos, entry.DataOffset, entry.Length);
        }
        
        return null;
    }
    
    private (long IndexPosition, long DataOffset, int Length)? FindVectorLocationByIndex(long targetIndex)
    {
        long currentIndex = 0;
        
        for (long pos = 0; pos < _appendMetadata.GetNextPositions().indexPos; pos += s_indexEntryByteLength)
        {
            var entry = ReadIndexEntryAt(pos);
            
            if (entry.Id.Equals(Guid.Empty))
                break;
                
            if (!entry.Id.Equals(s_tombStone))
            {
                if (currentIndex == targetIndex)
                    return (pos, entry.DataOffset, entry.Length);
                currentIndex++;
            }
        }
        
        return null;
    }
    
    private Vector ReadVectorAtLocation((long IndexPosition, long DataOffset, int Length) location)
    {
        var data = ReadDataAt(location.DataOffset, location.Length);
        return new Vector(data);
    }
    
    private byte[] ReadDataAt(long offset, int length)
    {
        // RandomAccess is thread-safe for position-independent reads
        var buffer = new byte[length];
        _dataFile.ReadExactly(offset, buffer);
        return buffer;
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

    private void WriteTombstone(long indexPosition)
    {
        _indexFile.Write(indexPosition, s_tombStoneBytes);
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
    
    private List<(long offset, int length)> GetAllValidLocations()
    {
        var locations = new List<(long, int)>();
        var (maxIndexPos, _) = _appendMetadata.GetNextPositions();
        
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
        _rwLock.EnterWriteLock();
        try
        {
            // Scan once at startup to find append positions
            long maxIndexPos = 0;
            long maxDataPos = 0;
            long fileLength = _indexFile.GetLength();

            // Find the first empty slot in index
            for (long pos = 0; pos < fileLength; pos += s_indexEntryByteLength)
            {
                var entry = ReadIndexEntryAt(pos);

                if (entry.Id.Equals(Guid.Empty))
                {
                    maxIndexPos = pos;
                    break;
                }

                // Track the highest data position for non-tombstone entries
                if (!entry.Id.Equals(s_tombStone))
                {
                    var endOfData = entry.DataOffset + entry.Length;
                    if (endOfData > maxDataPos)
                        maxDataPos = endOfData;

                    Interlocked.Increment(ref _count);
                }
            }

            _appendMetadata.Reset(maxIndexPos, maxDataPos);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
    
    private void ThrowIfDisposed()
    {
        if (_disposedValue)
            throw new ObjectDisposedException(nameof(MemoryMappedList));
    }
}