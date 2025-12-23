using Microsoft.Win32.SafeHandles;

namespace Neighborly;

internal enum WALOperationType
{
    Add,
    Remove,
    Update
}

internal struct WALEntry
{
    public long LSN { get; set; }
    public WALOperationType Operation { get; set; }
    public Guid VectorId { get; set; }
    public byte[]? VectorData { get; set; }
    public long IndexPosition { get; set; }
    public long DataPosition { get; set; }
    public DateTime Timestamp { get; set; }
}

internal class WriteAheadLog : IDisposable
{
    // Entry layout constants
    private const int LsnSize = sizeof(long);
    private const int OperationSize = sizeof(byte);
    private const int GuidSize = 16;
    private const int DataLengthSize = sizeof(int);
    private const int PositionSize = sizeof(long);
    private const int TimestampSize = sizeof(long);
    private const int FixedEntrySize = LsnSize + OperationSize + GuidSize + DataLengthSize + PositionSize + PositionSize + TimestampSize;

    private readonly string _walPath;
    private SafeFileHandle? _walHandle;
    private readonly SemaphoreSlim _walLock = new(1, 1);
    private bool _disposedValue;

    private long _nextLSN;
    private long _checkpointedLSN;
    private long _writePosition;

    /// <summary>
    /// Gets the next LSN that will be assigned to an operation.
    /// </summary>
    public long NextLSN => _nextLSN;

    /// <summary>
    /// Gets the last checkpointed LSN. All operations up to this LSN have been durably flushed.
    /// </summary>
    public long CheckpointedLSN => _checkpointedLSN;

    public WriteAheadLog(string basePath)
    {
        _walPath = Path.ChangeExtension(basePath, ".wal");

        // Use OpenOrCreate to preserve existing WAL for recovery
        _walHandle = File.OpenHandle(
            _walPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            FileOptions.RandomAccess);

        // Initialize LSN from existing entries
        InitializeLSNFromExistingEntries();

        // Set write position to end of file for appending
        _writePosition = RandomAccess.GetLength(_walHandle);
    }

    private void InitializeLSNFromExistingEntries()
    {
        var entries = ReadEntriesInternal();
        if (entries.Count > 0)
        {
            _nextLSN = entries.Max(e => e.LSN) + 1;
            _checkpointedLSN = 0; // Will be set during recovery
        }
        else
        {
            _nextLSN = 1;
            _checkpointedLSN = 0;
        }
    }

    /// <summary>
    /// Logs an operation to the WAL and returns the assigned LSN.
    /// The WAL is flushed to disk before returning.
    /// </summary>
    public long LogOperation(WALOperationType operation, Guid vectorId, byte[]? vectorData, long indexPosition, long dataPosition)
    {
        _walLock.Wait();
        try
        {
            return LogOperationCore(operation, vectorId, vectorData, indexPosition, dataPosition);
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously logs an operation to the WAL and returns the assigned LSN.
    /// The WAL is flushed to disk before returning.
    /// </summary>
    public async ValueTask<long> LogOperationAsync(WALOperationType operation, Guid vectorId, byte[]? vectorData, long indexPosition, long dataPosition, CancellationToken cancellationToken = default)
    {
        await _walLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LogOperationCoreAsync(operation, vectorId, vectorData, indexPosition, dataPosition, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _walLock.Release();
        }
    }

    private long LogOperationCore(WALOperationType operation, Guid vectorId, byte[]? vectorData, long indexPosition, long dataPosition)
    {
        long lsn = _nextLSN++;

        var entry = new WALEntry
        {
            LSN = lsn,
            Operation = operation,
            VectorId = vectorId,
            VectorData = vectorData,
            IndexPosition = indexPosition,
            DataPosition = dataPosition,
            Timestamp = DateTime.UtcNow
        };

        WriteEntry(entry);
        RandomAccess.FlushToDisk(_walHandle!);

        return lsn;
    }

    private async ValueTask<long> LogOperationCoreAsync(WALOperationType operation, Guid vectorId, byte[]? vectorData, long indexPosition, long dataPosition, CancellationToken cancellationToken)
    {
        long lsn = _nextLSN++;

        var entry = new WALEntry
        {
            LSN = lsn,
            Operation = operation,
            VectorId = vectorId,
            VectorData = vectorData,
            IndexPosition = indexPosition,
            DataPosition = dataPosition,
            Timestamp = DateTime.UtcNow
        };

        await WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        await Task.Run(() => RandomAccess.FlushToDisk(_walHandle!), cancellationToken).ConfigureAwait(false);

        return lsn;
    }

    private void WriteEntry(WALEntry entry)
    {
        var buffer = SerializeEntry(entry);
        RandomAccess.Write(_walHandle!, buffer, _writePosition);
        _writePosition += buffer.Length;
    }

    private async ValueTask WriteEntryAsync(WALEntry entry, CancellationToken cancellationToken)
    {
        var buffer = SerializeEntry(entry);
        await RandomAccess.WriteAsync(_walHandle!, buffer, _writePosition, cancellationToken).ConfigureAwait(false);
        _writePosition += buffer.Length;
    }

    private static byte[] SerializeEntry(WALEntry entry)
    {
        int vectorDataLength = entry.VectorData?.Length ?? 0;
        int totalSize = FixedEntrySize + vectorDataLength;
        var buffer = new byte[totalSize];
        int offset = 0;

        // LSN (8 bytes)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), entry.LSN);
        offset += LsnSize;

        // Operation (1 byte)
        buffer[offset++] = (byte)entry.Operation;

        // VectorId (16 bytes)
        entry.VectorId.TryWriteBytes(buffer.AsSpan(offset, GuidSize));
        offset += GuidSize;

        // VectorData length (4 bytes)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), vectorDataLength);
        offset += DataLengthSize;

        // VectorData (variable)
        if (entry.VectorData != null)
        {
            entry.VectorData.CopyTo(buffer.AsSpan(offset));
            offset += vectorDataLength;
        }

        // IndexPosition (8 bytes)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), entry.IndexPosition);
        offset += PositionSize;

        // DataPosition (8 bytes)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), entry.DataPosition);
        offset += PositionSize;

        // Timestamp (8 bytes)
        BitConverter.TryWriteBytes(buffer.AsSpan(offset), entry.Timestamp.ToBinary());

        return buffer;
    }

    /// <summary>
    /// Checkpoints the WAL up to the specified LSN, removing all entries with LSN &lt;= checkpointLSN.
    /// This should only be called after the corresponding data has been durably flushed to disk.
    /// </summary>
    public void Checkpoint(long checkpointLSN)
    {
        _walLock.Wait();
        try
        {
            CheckpointCore(checkpointLSN);
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously checkpoints the WAL up to the specified LSN.
    /// </summary>
    public async ValueTask CheckpointAsync(long checkpointLSN, CancellationToken cancellationToken = default)
    {
        await _walLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CheckpointCoreAsync(checkpointLSN, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _walLock.Release();
        }
    }

    private void CheckpointCore(long checkpointLSN)
    {
        if (checkpointLSN <= _checkpointedLSN)
            return; // Already checkpointed

        _checkpointedLSN = checkpointLSN;
        TruncateToCheckpoint();
    }

    private async ValueTask CheckpointCoreAsync(long checkpointLSN, CancellationToken cancellationToken)
    {
        if (checkpointLSN <= _checkpointedLSN)
            return; // Already checkpointed

        _checkpointedLSN = checkpointLSN;
        await TruncateToCheckpointAsync(cancellationToken).ConfigureAwait(false);
    }

    private void TruncateToCheckpoint()
    {
        // Read remaining entries (after checkpoint)
        var allEntries = ReadEntriesInternal();
        var remainingEntries = allEntries.Where(e => e.LSN > _checkpointedLSN).ToList();

        // Truncate file
        RandomAccess.SetLength(_walHandle!, 0);
        _writePosition = 0;

        // Rewrite remaining entries
        foreach (var entry in remainingEntries)
        {
            WriteEntry(entry);
        }

        RandomAccess.FlushToDisk(_walHandle!);
    }

    private async ValueTask TruncateToCheckpointAsync(CancellationToken cancellationToken)
    {
        // Read remaining entries (after checkpoint)
        var allEntries = await ReadEntriesInternalAsync(cancellationToken).ConfigureAwait(false);
        var remainingEntries = allEntries.Where(e => e.LSN > _checkpointedLSN).ToList();

        // Truncate file
        RandomAccess.SetLength(_walHandle!, 0);
        _writePosition = 0;

        // Rewrite remaining entries
        foreach (var entry in remainingEntries)
        {
            await WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        await Task.Run(() => RandomAccess.FlushToDisk(_walHandle!), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the entire WAL. This is deprecated - use Checkpoint() instead for proper ACID guarantees.
    /// </summary>
    [Obsolete("Use Checkpoint(lsn) instead for proper ACID guarantees")]
    public void Commit()
    {
        _walLock.Wait();
        try
        {
            RandomAccess.SetLength(_walHandle!, 0);
            _writePosition = 0;
            RandomAccess.FlushToDisk(_walHandle!);
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Reads all uncheckpointed entries from the WAL.
    /// </summary>
    public List<WALEntry> ReadEntries()
    {
        _walLock.Wait();
        try
        {
            return ReadEntriesInternal().Where(e => e.LSN > _checkpointedLSN).ToList();
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously reads all uncheckpointed entries from the WAL.
    /// </summary>
    public async ValueTask<List<WALEntry>> ReadEntriesAsync(CancellationToken cancellationToken = default)
    {
        await _walLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = await ReadEntriesInternalAsync(cancellationToken).ConfigureAwait(false);
            return entries.Where(e => e.LSN > _checkpointedLSN).ToList();
        }
        finally
        {
            _walLock.Release();
        }
    }

    /// <summary>
    /// Reads all entries from the WAL file without filtering by checkpoint.
    /// </summary>
    private List<WALEntry> ReadEntriesInternal()
    {
        var entries = new List<WALEntry>();

        if (_walHandle == null)
            return entries;

        long fileLength = RandomAccess.GetLength(_walHandle);
        if (fileLength == 0)
            return entries;

        long position = 0;
        var headerBuffer = new byte[FixedEntrySize];

        while (position < fileLength)
        {
            try
            {
                // Read fixed-size header
                int headerRead = RandomAccess.Read(_walHandle, headerBuffer, position);
                if (headerRead < FixedEntrySize)
                    break;

                var entry = new WALEntry();
                int offset = 0;

                // LSN
                entry.LSN = BitConverter.ToInt64(headerBuffer.AsSpan(offset, LsnSize));
                offset += LsnSize;

                // Operation
                entry.Operation = (WALOperationType)headerBuffer[offset++];

                // VectorId
                entry.VectorId = new Guid(headerBuffer.AsSpan(offset, GuidSize));
                offset += GuidSize;

                // VectorData length
                int dataLength = BitConverter.ToInt32(headerBuffer.AsSpan(offset, DataLengthSize));
                offset += DataLengthSize;

                position += offset;

                // VectorData (variable)
                if (dataLength > 0)
                {
                    var dataBuffer = new byte[dataLength];
                    int dataRead = RandomAccess.Read(_walHandle, dataBuffer, position);
                    if (dataRead < dataLength)
                        break;
                    entry.VectorData = dataBuffer;
                    position += dataLength;
                }

                // Read remaining fixed fields
                var tailBuffer = new byte[PositionSize + PositionSize + TimestampSize];
                int tailRead = RandomAccess.Read(_walHandle, tailBuffer, position);
                if (tailRead < tailBuffer.Length)
                    break;

                int tailOffset = 0;
                entry.IndexPosition = BitConverter.ToInt64(tailBuffer.AsSpan(tailOffset, PositionSize));
                tailOffset += PositionSize;
                entry.DataPosition = BitConverter.ToInt64(tailBuffer.AsSpan(tailOffset, PositionSize));
                tailOffset += PositionSize;
                entry.Timestamp = DateTime.FromBinary(BitConverter.ToInt64(tailBuffer.AsSpan(tailOffset, TimestampSize)));

                position += tailBuffer.Length;

                entries.Add(entry);
            }
            catch (Exception)
            {
                break;
            }
        }

        return entries;
    }

    /// <summary>
    /// Asynchronously reads all entries from the WAL file without filtering by checkpoint.
    /// </summary>
    private async ValueTask<List<WALEntry>> ReadEntriesInternalAsync(CancellationToken cancellationToken)
    {
        var entries = new List<WALEntry>();

        if (_walHandle == null)
            return entries;

        long fileLength = RandomAccess.GetLength(_walHandle);
        if (fileLength == 0)
            return entries;

        long position = 0;
        var headerBuffer = new byte[FixedEntrySize];

        while (position < fileLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Read fixed-size header
                int headerRead = await RandomAccess.ReadAsync(_walHandle, headerBuffer, position, cancellationToken).ConfigureAwait(false);
                if (headerRead < FixedEntrySize)
                    break;

                var entry = new WALEntry();
                int offset = 0;

                // LSN
                entry.LSN = BitConverter.ToInt64(headerBuffer.AsSpan(offset, LsnSize));
                offset += LsnSize;

                // Operation
                entry.Operation = (WALOperationType)headerBuffer[offset++];

                // VectorId
                entry.VectorId = new Guid(headerBuffer.AsSpan(offset, GuidSize));
                offset += GuidSize;

                // VectorData length
                int dataLength = BitConverter.ToInt32(headerBuffer.AsSpan(offset, DataLengthSize));
                offset += DataLengthSize;

                position += offset;

                // VectorData (variable)
                if (dataLength > 0)
                {
                    var dataBuffer = new byte[dataLength];
                    int dataRead = await RandomAccess.ReadAsync(_walHandle, dataBuffer, position, cancellationToken).ConfigureAwait(false);
                    if (dataRead < dataLength)
                        break;
                    entry.VectorData = dataBuffer;
                    position += dataLength;
                }

                // Read remaining fixed fields
                var tailBuffer = new byte[PositionSize + PositionSize + TimestampSize];
                int tailRead = await RandomAccess.ReadAsync(_walHandle, tailBuffer, position, cancellationToken).ConfigureAwait(false);
                if (tailRead < tailBuffer.Length)
                    break;

                int tailOffset = 0;
                entry.IndexPosition = BitConverter.ToInt64(tailBuffer.AsSpan(tailOffset, PositionSize));
                tailOffset += PositionSize;
                entry.DataPosition = BitConverter.ToInt64(tailBuffer.AsSpan(tailOffset, PositionSize));
                tailOffset += PositionSize;
                entry.Timestamp = DateTime.FromBinary(BitConverter.ToInt64(tailBuffer.AsSpan(tailOffset, TimestampSize)));

                position += tailBuffer.Length;

                entries.Add(entry);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                break;
            }
        }

        return entries;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _walHandle?.Dispose();
                _walHandle = null;
                _walLock?.Dispose();

                if (File.Exists(_walPath))
                {
                    try
                    {
                        File.Delete(_walPath);
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Warning(ex, "Failed to delete WAL file: {WalPath}", _walPath);
                    }
                }
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
