using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

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
    private readonly MemoryMappedFileHolder _indexFile;
    private readonly MemoryMappedFileHolder _dataFile;
    private readonly WriteAheadLog _wal;
    private readonly DurabilityManager _durabilityManager;
    private static readonly MemoryPressureMonitor s_memoryMonitor = new();
    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
    private long _count;
    /// <summary>
    /// Indicates if the index stream is at the end of the stream.
    /// This is used to enable fast adding of multiple vectors in sequence.
    /// </summary>
    private bool _isAtEndOfIndexStream = true;
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
    #pragma warning disable CS0414 // Field assigned but never used - reserved for future defragmentation
    private long _defragPosition = 0; // Tracks the current position in the index file for defragmentation
    #pragma warning restore CS0414
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
        _indexFile = new(s_indexEntryByteLength * capacity);
        // Based on typical vector dimensions, 4096 bytes should be enough for most cases as of 2024-06
        _dataFile = new(4096L * capacity);
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
        _isAtEndOfIndexStream = false;
        _indexFile.Stream.Seek(0, SeekOrigin.Begin);
        _dataFile.Stream.Seek(0, SeekOrigin.Begin);

        byte[] entry = new byte[s_indexEntryByteLength];
        int bytesRead;
        while ((bytesRead = _indexFile.Stream.Read(entry, 0, entry.Length)) > 0)
        {
            if (bytesRead != s_indexEntryByteLength)
            {
                throw new InvalidOperationException("Failed to read the index entry");
            }

            var entrySpan = entry.AsSpan();
            Guid id = new(entrySpan[..s_idBytesLength]);
            if (id.Equals(s_tombStone))
            {
                continue;
            }

            if (id.Equals(Guid.Empty) || id.Equals(s_tombStone))
            {
                _isAtEndOfIndexStream = true;
                break;
            }

            Span<byte> offsetBytes = entrySpan[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
            Span<byte> lengthBytes = entrySpan[(s_idBytesLength + s_offsetBytesLength)..];
            long offset = BitConverter.ToInt64(offsetBytes);
            int length = BitConverter.ToInt32(lengthBytes);

            _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
            byte[] bytes = new byte[length];
            _dataFile.Stream.ReadExactly(bytes);
            array[arrayIndex++] = new Vector(bytes);
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

    private void AddInternal(Vector vector)
    {
        // This method assumes the write lock is already held
        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        if (!_isAtEndOfIndexStream)
        {
            ReadToEnd();
        }

        long indexPosition = _indexFile.Stream.Position;
        long dataPosition = _dataFile.Stream.Position;
        byte[] data = vector.ToBinary();

        // Log operation before performing it
        _wal.LogOperation(WALOperationType.Add, vector.Id, data, indexPosition, dataPosition);

        Span<byte> idBytes = entry[..s_idBytesLength];
        if (!vector.Id.TryWriteBytes(idBytes))
        {
            throw new InvalidOperationException("Failed to write the Id to bytes");
        }

        Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
        if (!BitConverter.TryWriteBytes(offsetBytes, dataPosition))
        {
            throw new InvalidOperationException("Failed to write the offset to bytes");
        }

        Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
        if (!BitConverter.TryWriteBytes(lengthBytes, data.Length))
        {
            throw new InvalidOperationException("Failed to write the length to bytes");
        }

        _indexFile.Stream.Write(entry);
        _dataFile.Stream.Write(data);

        _isAtEndOfIndexStream = true;
        
        // Record operation for durability management
        _durabilityManager.RecordOperation();
        
        // Commit WAL after successful operation
        _wal.Commit();
        
        Interlocked.Increment(ref _count);
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
            // Find the vector in the index
            _isAtEndOfIndexStream = false;
            _indexFile.Stream.Seek(0, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;
            
            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                Guid id = new(entry[..s_idBytesLength]);
                if (id == vector.Id)
                {
                    // Found the vector - get current data position and length
                    long currentIndexPosition = _indexFile.Stream.Position - s_indexEntryByteLength;
                    long currentDataOffset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength));
                    int currentDataLength = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));
                    
                    byte[] newData = vector.ToBinary();
                    
                    // Try to reuse existing space if new data fits
                    long newDataPosition;
                    if (newData.Length <= currentDataLength)
                    {
                        // Reuse existing space
                        newDataPosition = currentDataOffset;
                        _dataFile.Stream.Seek(newDataPosition, SeekOrigin.Begin);
                    }
                    else
                    {
                        // Need more space - find the actual end of used data (not end of file)
                        newDataPosition = FindActualEndOfData();
                        _dataFile.Stream.Seek(newDataPosition, SeekOrigin.Begin);
                        
                        // Check if we have enough capacity
                        if (newDataPosition + newData.Length > _dataFile.Stream.Length)
                        {
                            // Log detailed capacity information for debugging
                            var actualUsedSpace = newDataPosition;
                            var totalSpace = _dataFile.Stream.Length;
                            var requestedSpace = newData.Length;
                            
                            // Insufficient capacity - return false to indicate failure
                            return false;
                        }
                    }

                    // Log operation before performing it
                    _wal.LogOperation(WALOperationType.Update, vector.Id, newData, currentIndexPosition, newDataPosition);

                    // Write new data
                    _dataFile.Stream.Write(newData);
                    
                    // Update index entry with new offset and length
                    _indexFile.Stream.Seek(currentIndexPosition + s_idBytesLength, SeekOrigin.Begin);
                    Span<byte> offsetBytes = stackalloc byte[s_offsetBytesLength];
                    Span<byte> lengthBytes = stackalloc byte[s_lengthBytesLength];
                    
                    if (!BitConverter.TryWriteBytes(offsetBytes, newDataPosition))
                    {
                        throw new InvalidOperationException("Failed to write the offset to bytes");
                    }
                    if (!BitConverter.TryWriteBytes(lengthBytes, newData.Length))
                    {
                        throw new InvalidOperationException("Failed to write the length to bytes");
                    }
                    
                    _indexFile.Stream.Write(offsetBytes);
                    _indexFile.Stream.Write(lengthBytes);
                    
                    // Record operation for durability management
                    _durabilityManager.RecordOperation();
                    
                    // Commit WAL after successful operation
                    _wal.Commit();
                    
                    // Count remains the same for update operation
                    return true;
                }
                else if (id.Equals(Guid.Empty) || id.Equals(s_tombStone))
                {
                    _isAtEndOfIndexStream = true;
                    ReverseIndexStreamByIdBytesLength();
                    break;
                }
            }

            return false;
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
            long expectedDataPosition = 0; // Expected start position of the next data entry
            long totalFragmentation = 0; // Total size of gaps between data entries
            long totalDataSize = 0; // Total size of data entries

            _indexFile.Stream.Seek(0, SeekOrigin.Begin); // Start from the beginning of the index file

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength]; // Buffer for reading index entries
            int bytesRead;

            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0) // Read each index entry
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    // If the read entry is incomplete, skip it and continue to the next entry
                    _indexFile.Stream.Seek(s_indexEntryByteLength - bytesRead, SeekOrigin.Current);
                    continue;
                }

                Guid id = new(entry[..s_idBytesLength]); // Extract the ID from the entry
                if (id.Equals(s_tombStone) || id.Equals(Guid.Empty))
                {
                    continue; // Skip tombstoned or empty entries
                }

                long actualOffset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength)); // Actual start position of the data entry
                int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength)); // Length of the data entry

                if (actualOffset > expectedDataPosition)
                {
                    // If there's a gap between the expected and actual position, it's fragmentation
                    long gapSize = actualOffset - expectedDataPosition;
                    totalFragmentation += gapSize;
                }

                // Update the expected position for the next entry
                expectedDataPosition = actualOffset + length;
                totalDataSize += length;
            }

            if (totalDataSize == 0)
            {
                return 0;
            }

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

            _indexFile.Stream.Seek(0, SeekOrigin.Begin);
            _dataFile.Stream.Seek(0, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;

            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                Guid id = new(entry[..s_idBytesLength]);
                if (id.Equals(s_tombStone))
                {
                    continue; // Skip tombstoned entries
                }

                if (id.Equals(Guid.Empty))
                {
                    break; // End of valid entries
                }

                long offset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength));
                int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));

                // Read data associated with the entry
                byte[] data = new byte[length];
                _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
                _dataFile.Stream.ReadExactly(data);

                // Update the offset in the index entry to the new data position
                BitConverter.TryWriteBytes(entry.Slice(s_idBytesLength, s_offsetBytesLength), newDataPosition);

                // Write the updated index entry back to the index file at the new position
                _indexFile.Stream.Seek(newIndexPosition * s_indexEntryByteLength, SeekOrigin.Begin);
                _indexFile.Stream.Write(entry);

                // Write the data back to the data file at the new position
                _dataFile.Stream.Seek(newDataPosition, SeekOrigin.Begin);
                _dataFile.Stream.Write(data);

                newIndexPosition++;
                newDataPosition += length;
            }
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
            long newIndexPosition = _defragIndexPosition;
            long newDataPosition = _newDataPosition;
            long totalDataSize = 0;
            long totalFragmentation = 0;
            int entriesProcessed = 0;

            _indexFile.Stream.Seek(newIndexPosition * s_indexEntryByteLength, SeekOrigin.Begin);
            _dataFile.Stream.Seek(newDataPosition, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;
            List<(Guid id, long oldOffset, int length, long newOffset)> updates = new List<(Guid, long, int, long)>(_defragBatchSize);

            // Find the maximum entry size in this batch
            int maxEntrySize = 0;

            long currentReadPosition = newIndexPosition;
            while (entriesProcessed < _defragBatchSize && currentReadPosition * s_indexEntryByteLength < _indexFile.Stream.Length)
            {
                _indexFile.Stream.Seek(currentReadPosition * s_indexEntryByteLength, SeekOrigin.Begin);
                bytesRead = _indexFile.Stream.Read(entry);
                if (bytesRead != s_indexEntryByteLength)
                {
                    break; // End of file or incomplete entry
                }

                Guid id = new(entry[..s_idBytesLength]);
                if (id.Equals(s_tombStone))
                {
                    currentReadPosition++;
                    continue; // Skip tombstoned entries but don't count them
                }

                if (id.Equals(Guid.Empty))
                {
                    break; // End of valid entries
                }

                long offset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength));
                int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));

                if (offset > newDataPosition)
                {
                    totalFragmentation += offset - newDataPosition;
                }

                updates.Add((id, offset, length, newDataPosition));
                maxEntrySize = Math.Max(maxEntrySize, length);

                newDataPosition += length;
                totalDataSize += length;
                entriesProcessed++;
                currentReadPosition++;
            }

            newIndexPosition = currentReadPosition;

            // Rent a buffer from the ArrayPool
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(maxEntrySize);

            try
            {
                // Allocate entry buffer outside the loop
                Span<byte> updatedEntry = stackalloc byte[s_indexEntryByteLength];
                
                // Perform all reads and writes
                foreach (var update in updates)
                {
                    // Read data from old position
                    _dataFile.Stream.Seek(update.oldOffset, SeekOrigin.Begin);
                    _dataFile.Stream.ReadExactly(sharedBuffer, 0, update.length);

                    // Write data to new position
                    _dataFile.Stream.Seek(update.newOffset, SeekOrigin.Begin);
                    _dataFile.Stream.Write(sharedBuffer, 0, update.length);

                    // Update index entry - write to the correct defrag position
                    long writePosition = _defragIndexPosition * s_indexEntryByteLength;
                    _indexFile.Stream.Seek(writePosition, SeekOrigin.Begin);
                    if (!update.id.TryWriteBytes(updatedEntry[..s_idBytesLength]))
                    {
                        throw new InvalidOperationException("Failed to write the Id to bytes");
                    }
                    
                    if (!BitConverter.TryWriteBytes(updatedEntry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)], update.newOffset))
                    {
                        throw new InvalidOperationException("Failed to write the offset to bytes");
                    }
                    
                    if (!BitConverter.TryWriteBytes(updatedEntry[(s_idBytesLength + s_offsetBytesLength)..], update.length))
                    {
                        throw new InvalidOperationException("Failed to write the length to bytes");
                    }
                    
                    _indexFile.Stream.Write(updatedEntry);
                    _defragIndexPosition++;
                }
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }

            // Update tracking variables for the next batch
            _newDataPosition = newDataPosition;

            // Detecting the end of defragmentation
            if (entriesProcessed == 0 || newIndexPosition * s_indexEntryByteLength >= _indexFile.Stream.Length)
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
            _indexFile.DisposeStreams();
            _indexFile.Reset();
            _dataFile.DisposeStreams();
            _dataFile.Reset();
            Interlocked.Exchange(ref _count, 0);
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
            // This is more appropriate for concurrency scenarios where we care about object identity
            // Inline the search logic to avoid lock recursion
            _indexFile.Stream.Seek(0, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;
            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                Guid vectorId = new(entry[..s_idBytesLength]);
                if (item.Id == vectorId)
                {
                    return true;
                }
                else if (vectorId.Equals(Guid.Empty))
                {
                    // Hit the end of actual entries, stop searching
                    break;
                }
                else if (vectorId.Equals(s_tombStone))
                {
                    // Skip tombstones but continue searching
                    continue;
                }
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
        _isAtEndOfIndexStream = false;
        _indexFile.Stream.Seek(0, SeekOrigin.Begin);

        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        int bytesRead;
        long index = 0L;
        while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
        {
            if (bytesRead != s_indexEntryByteLength)
            {
                throw new InvalidOperationException("Failed to read the index entry");
            }

            Guid vectorId = new(entry[..s_idBytesLength]);
            if (id == vectorId)
            {
                Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
                long offset = BitConverter.ToInt64(offsetBytes);
                int length = BitConverter.ToInt32(lengthBytes);

                return (index, offset, length);
            }
            else if (vectorId.Equals(Guid.Empty))
            {
                // Hit the end of actual entries, stop searching
                _isAtEndOfIndexStream = true;
                ReverseIndexStreamByIdBytesLength();
                break;
            }
            else if (vectorId.Equals(s_tombStone))
            {
                // Skip tombstones but continue searching
                ++index;
                continue;
            }

            ++index;
        }

        return (-1L, -1L, -1);
    }

    /// <summary>
    /// Reads the index file and the data file to the last entry,
    /// so that new data can be appended safely.
    /// </summary>
    /// <summary>
    /// Determines the correct positions for appending new data to both index and data files.
    /// This method is thread-safe and should be called within a write lock.
    /// </summary>
    /// <param name="indexPosition">The position in the index file where the new entry should be written</param>
    /// <param name="dataPosition">The position in the data file where the new vector data should be written</param>
    private void DetermineAppendPositions(out long indexPosition, out long dataPosition)
    {
        _rwLock.EnterReadLock();
        try
        {
            // Find the end of the index file by scanning for the first empty or tombstone entry
            _indexFile.Stream.Seek(0, SeekOrigin.Begin);
            
            long maxDataOffset = -1L;
            int lengthAtMaxOffset = -1;
            indexPosition = 0; // Initialize to start of file
            dataPosition = 0;  // Initialize to start of file

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;
            bool foundEmptySlot = false;
            
            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                long currentIndexPos = _indexFile.Stream.Position - s_indexEntryByteLength;
                Guid vectorId = new(entry[..s_idBytesLength]);
                
                if (vectorId.Equals(Guid.Empty) || vectorId.Equals(s_tombStone))
                {
                    // Found end of valid entries or a reusable tombstone slot
                    if (!foundEmptySlot || vectorId.Equals(Guid.Empty))
                    {
                        indexPosition = currentIndexPos;
                        foundEmptySlot = true;
                        if (vectorId.Equals(Guid.Empty))
                        {
                            // Hit the actual end, stop scanning
                            break;
                        }
                    }
                }
                else
                {
                    // This is a valid entry, track the highest data position
                    Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                    Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
                    long offset = BitConverter.ToInt64(offsetBytes);
                    int length = BitConverter.ToInt32(lengthBytes);

                    if (offset > maxDataOffset || (offset == maxDataOffset && length > lengthAtMaxOffset))
                    {
                        maxDataOffset = offset;
                        lengthAtMaxOffset = length;
                    }
                }
            }
            
            // If we didn't find any empty slot during the scan, append at the end
            if (!foundEmptySlot)
            {
                indexPosition = _indexFile.Stream.Position;
            }
            
            // Data position is after the last written data
            dataPosition = maxDataOffset == -1L ? 0L : maxDataOffset + lengthAtMaxOffset;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    private void ReadToEnd()
    {
        _indexFile.Stream.Seek(0, SeekOrigin.Begin);

        // To keep track of the current position in the data file
        // that we can use to append new data.
        // Theoretically, this should always be the last records offset + length,
        // but we can't be sure that the index file is always in sync with the data file.
        long maxOffset = -1L;
        int lengthAtMaxOffset = -1;

        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        int bytesRead;
        while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
        {
            if (bytesRead != s_indexEntryByteLength)
            {
                throw new InvalidOperationException("Failed to read the index entry");
            }

            Guid vectorId = new(entry[..s_idBytesLength]);
            if (!vectorId.Equals(Guid.Empty) && !vectorId.Equals(s_tombStone))
            {
                Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
                long offset = BitConverter.ToInt64(offsetBytes);
                int length = BitConverter.ToInt32(lengthBytes);

                if (offset > maxOffset || (offset == maxOffset && length > lengthAtMaxOffset))
                {
                    maxOffset = offset;
                    lengthAtMaxOffset = length;
                }
            }
            else
            {
                _isAtEndOfIndexStream = true;
                ReverseIndexStreamByIdBytesLength();
                _dataFile.Stream.Seek(maxOffset + lengthAtMaxOffset, SeekOrigin.Begin);
                break;
            }
        }
    }

    /// <summary>
    /// If the index stream is not at the beginning, it will be moved to the previous entry.
    /// This is to go back one entry after searching for an entry and getting an empty entry.
    /// </summary>
    private void ReverseIndexStreamByIdBytesLength()
    {
        if (_indexFile.Stream.Position != 0L)
        {
            _indexFile.Stream.Seek(-s_indexEntryByteLength, SeekOrigin.Current);
        }
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

    /// <summary>
    /// Finds the actual end of used data by scanning the index for the maximum offset + length
    /// </summary>
    private long FindActualEndOfData()
    {
        long maxEndPosition = 0;
        long currentIndexPosition = _indexFile.Stream.Position; // Save current position
        
        _indexFile.Stream.Seek(0, SeekOrigin.Begin);
        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        
        while (_indexFile.Stream.Read(entry) == s_indexEntryByteLength)
        {
            Guid id = new(entry[..s_idBytesLength]);
            
            // Skip tombstones and empty entries
            if (id.Equals(s_tombStone) || id.Equals(Guid.Empty))
            {
                if (id.Equals(Guid.Empty))
                    break; // End of valid entries
                continue;
            }
            
            long offset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength));
            int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));
            
            long endPosition = offset + length;
            if (endPosition > maxEndPosition)
            {
                maxEndPosition = endPosition;
            }
        }
        
        _indexFile.Stream.Seek(currentIndexPosition, SeekOrigin.Begin); // Restore position
        return maxEndPosition;
    }

    internal void ReleaseMappedMemory()
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Dispose and recreate streams to release memory-mapped memory
            // This allows the OS to page out unused data under memory pressure
            _indexFile.DisposeStreams();
            _dataFile.DisposeStreams();
            
            // Recreate the memory-mapped files
            _indexFile.Reset();
            _dataFile.Reset();
            
            _isAtEndOfIndexStream = false; // Force recalculation of stream position
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
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
        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        if (!_isAtEndOfIndexStream)
        {
            ReadToEnd();
        }

        Span<byte> idBytes = entry[..s_idBytesLength];
        if (!vector.Id.TryWriteBytes(idBytes))
        {
            throw new InvalidOperationException("Failed to write the Id to bytes");
        }

        Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
        if (!BitConverter.TryWriteBytes(offsetBytes, _dataFile.Stream.Position))
        {
            throw new InvalidOperationException("Failed to write the offset to bytes");
        }

        byte[] data = vector.ToBinary();
        Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
        if (!BitConverter.TryWriteBytes(lengthBytes, data.Length))
        {
            throw new InvalidOperationException("Failed to write the length to bytes");
        }

        _indexFile.Stream.Write(entry);
        _dataFile.Stream.Write(data);

        _isAtEndOfIndexStream = true;
        Interlocked.Increment(ref _count);
    }

    private void ValidateFileIntegrity()
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Quick validation for empty files
            if (_indexFile.Stream.Length <= 0 && 
                _dataFile.Stream.Length <= 0)
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
                _indexFile.Stream.Seek(0, SeekOrigin.Begin);
                
                Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
                while (_indexFile.Stream.Position + s_indexEntryByteLength <= _indexFile.Stream.Length)
                {
                    if (_indexFile.Stream.Read(entry) != s_indexEntryByteLength)
                        break;
                    
                    Guid id = new(entry[..s_idBytesLength]);
                    if (id.Equals(Guid.Empty))
                        break;
                    
                    if (!id.Equals(s_tombStone))
                    {
                        count++;
                    }
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
        return Math.Max(0, _dataFile.Stream.Length - 0);
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
        // Use position-independent read with proper synchronization
        var buffer = new byte[length];
        lock (_dataFile.Stream)
        {
            _dataFile.Stream.Position = offset;
            _dataFile.Stream.ReadExactly(buffer);
        }
        return buffer;
    }
    
    private (Guid Id, long DataOffset, int Length) ReadIndexEntryAt(long position)
    {
        var buffer = new byte[s_indexEntryByteLength];
        lock (_indexFile.Stream)
        {
            _indexFile.Stream.Position = position;
            _indexFile.Stream.ReadExactly(buffer);
        }
        
        var id = new Guid(buffer.AsSpan(0, s_idBytesLength));
        var offset = BitConverter.ToInt64(buffer, s_idBytesLength);
        var length = BitConverter.ToInt32(buffer, s_idBytesLength + s_offsetBytesLength);
        
        return (id, offset, length);
    }
    
    private void WriteVectorData(long position, byte[] data)
    {
        lock (_dataFile.Stream)
        {
            _dataFile.Stream.Position = position;
            _dataFile.Stream.Write(data);
        }
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
        
        lock (_indexFile.Stream)
        {
            _indexFile.Stream.Position = position;
            _indexFile.Stream.Write(entry);
        }
    }
    
    private void WriteTombstone(long indexPosition)
    {
        lock (_indexFile.Stream)
        {
            _indexFile.Stream.Position = indexPosition;
            _indexFile.Stream.Write(s_tombStoneBytes);
        }
    }
    
    private void UpdateIndexEntry(long position, long newDataOffset, int newDataLength)
    {
        Span<byte> buffer = stackalloc byte[s_offsetBytesLength + s_lengthBytesLength];
        
        if (!BitConverter.TryWriteBytes(buffer[..s_offsetBytesLength], newDataOffset))
            throw new InvalidOperationException("Failed to write offset to bytes");
            
        if (!BitConverter.TryWriteBytes(buffer[s_offsetBytesLength..], newDataLength))
            throw new InvalidOperationException("Failed to write length to bytes");
        
        lock (_indexFile.Stream)
        {
            _indexFile.Stream.Position = position + s_idBytesLength;
            _indexFile.Stream.Write(buffer);
        }
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
            
            // Find the first empty slot in index
            for (long pos = 0; pos < _indexFile.Stream.Length; pos += s_indexEntryByteLength)
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