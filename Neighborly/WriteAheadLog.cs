using System.Text.Json;

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
    private readonly string _walPath;
    private FileStream _walStream;
    private BinaryWriter _walWriter;
    private readonly ReaderWriterLockSlim _walLock = new();
    private bool _disposedValue;

    private long _nextLSN;
    private long _checkpointedLSN;

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
        _walStream = new FileStream(_walPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        // Initialize LSN from existing entries
        InitializeLSNFromExistingEntries();

        // Position at end for appending new entries
        _walStream.Seek(0, SeekOrigin.End);
        _walWriter = new BinaryWriter(_walStream, System.Text.Encoding.UTF8, leaveOpen: true);
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
        _walLock.EnterWriteLock();
        try
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
            _walStream.Flush(flushToDisk: true); // fsync to ensure durability

            return lsn;
        }
        finally
        {
            _walLock.ExitWriteLock();
        }
    }

    private void WriteEntry(WALEntry entry)
    {
        _walWriter.Write(entry.LSN);
        _walWriter.Write((byte)entry.Operation);
        _walWriter.Write(entry.VectorId.ToByteArray());
        _walWriter.Write(entry.VectorData?.Length ?? 0);
        if (entry.VectorData != null)
        {
            _walWriter.Write(entry.VectorData);
        }
        _walWriter.Write(entry.IndexPosition);
        _walWriter.Write(entry.DataPosition);
        _walWriter.Write(entry.Timestamp.ToBinary());
    }

    /// <summary>
    /// Checkpoints the WAL up to the specified LSN, removing all entries with LSN &lt;= checkpointLSN.
    /// This should only be called after the corresponding data has been durably flushed to disk.
    /// </summary>
    public void Checkpoint(long checkpointLSN)
    {
        _walLock.EnterWriteLock();
        try
        {
            if (checkpointLSN <= _checkpointedLSN)
                return; // Already checkpointed

            _checkpointedLSN = checkpointLSN;
            TruncateToCheckpoint();
        }
        finally
        {
            _walLock.ExitWriteLock();
        }
    }

    private void TruncateToCheckpoint()
    {
        // Read remaining entries (after checkpoint)
        var allEntries = ReadEntriesInternal();
        var remainingEntries = allEntries.Where(e => e.LSN > _checkpointedLSN).ToList();

        // Flush and recreate writer (don't dispose - it would close the stream)
        _walWriter.Flush();

        // Truncate and rewrite with only remaining entries
        _walStream.SetLength(0);
        _walStream.Seek(0, SeekOrigin.Begin);

        // Create a new writer on the same stream
        _walWriter = new BinaryWriter(_walStream, System.Text.Encoding.UTF8, leaveOpen: true);

        foreach (var entry in remainingEntries)
        {
            WriteEntry(entry);
        }

        _walStream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Clears the entire WAL. This is deprecated - use Checkpoint() instead for proper ACID guarantees.
    /// </summary>
    [Obsolete("Use Checkpoint(lsn) instead for proper ACID guarantees")]
    public void Commit()
    {
        _walLock.EnterWriteLock();
        try
        {
            _walWriter.Flush();
            _walStream.SetLength(0);
            _walStream.Seek(0, SeekOrigin.Begin);
            _walWriter = new BinaryWriter(_walStream, System.Text.Encoding.UTF8, leaveOpen: true);
            _walStream.Flush();
        }
        finally
        {
            _walLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Reads all uncheckpointed entries from the WAL.
    /// </summary>
    public List<WALEntry> ReadEntries()
    {
        _walLock.EnterReadLock();
        try
        {
            return ReadEntriesInternal().Where(e => e.LSN > _checkpointedLSN).ToList();
        }
        finally
        {
            _walLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Reads all entries from the WAL file without filtering by checkpoint.
    /// </summary>
    private List<WALEntry> ReadEntriesInternal()
    {
        var entries = new List<WALEntry>();

        if (!File.Exists(_walPath) || new FileInfo(_walPath).Length == 0)
            return entries;

        using var readStream = new FileStream(_walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(readStream);

        while (readStream.Position < readStream.Length)
        {
            try
            {
                var entry = new WALEntry
                {
                    LSN = reader.ReadInt64(),
                    Operation = (WALOperationType)reader.ReadByte(),
                    VectorId = new Guid(reader.ReadBytes(16)),
                };

                int dataLength = reader.ReadInt32();
                entry.VectorData = dataLength > 0 ? reader.ReadBytes(dataLength) : null;
                entry.IndexPosition = reader.ReadInt64();
                entry.DataPosition = reader.ReadInt64();
                entry.Timestamp = DateTime.FromBinary(reader.ReadInt64());

                entries.Add(entry);
            }
            catch (EndOfStreamException)
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
                _walWriter?.Dispose();
                _walStream?.Dispose();
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