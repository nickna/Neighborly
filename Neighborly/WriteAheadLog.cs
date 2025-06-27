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
    private readonly FileStream _walStream;
    private readonly BinaryWriter _walWriter;
    private readonly ReaderWriterLockSlim _walLock = new();
    private bool _disposedValue;

    public WriteAheadLog(string basePath)
    {
        _walPath = Path.ChangeExtension(basePath, ".wal");
        _walStream = new FileStream(_walPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _walWriter = new BinaryWriter(_walStream);
    }

    public void LogOperation(WALOperationType operation, Guid vectorId, byte[]? vectorData, long indexPosition, long dataPosition)
    {
        _walLock.EnterWriteLock();
        try
        {
            var entry = new WALEntry
            {
                Operation = operation,
                VectorId = vectorId,
                VectorData = vectorData,
                IndexPosition = indexPosition,
                DataPosition = dataPosition,
                Timestamp = DateTime.UtcNow
            };

            WriteEntry(entry);
            _walStream.Flush();
        }
        finally
        {
            _walLock.ExitWriteLock();
        }
    }

    private void WriteEntry(WALEntry entry)
    {
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

    public void Commit()
    {
        _walLock.EnterWriteLock();
        try
        {
            _walStream.SetLength(0);
            _walStream.Flush();
        }
        finally
        {
            _walLock.ExitWriteLock();
        }
    }

    public List<WALEntry> ReadEntries()
    {
        _walLock.EnterReadLock();
        try
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
        finally
        {
            _walLock.ExitReadLock();
        }
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