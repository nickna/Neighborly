using System.Collections;
using System.IO.MemoryMappedFiles;

namespace Neighborly;

public class MemoryMappedList : IDisposable, IEnumerable<Vector>
{
    private const int s_idBytesLength = 16;
    private const int s_offsetBytesLength = sizeof(long);
    private const int s_lengthBytesLength = sizeof(int);
    private const int s_indexEntryByteLength = s_idBytesLength + s_offsetBytesLength + s_lengthBytesLength;
    private static readonly Guid s_tombStone;
    private static readonly byte[] s_tombStoneBytes;
    private readonly MemoryMappedFileHolder _indexFile;
    private readonly MemoryMappedFileHolder _dataFile;
    private readonly object _mutex = new();
    private long _count;
    private bool _disposedValue;

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

    public MemoryMappedList(long capacity)
    {
        _indexFile = new(s_indexEntryByteLength * capacity);
        // Based on typical vector dimensions, 4096 bytes should be enough for most cases as of 2024-06
        _dataFile = new(4096L * capacity);
    }

    public long Count
    {
        get
        {
            lock (_mutex)
            {
                return _count;
            }
        }
    }

#pragma warning disable CA1822 // Mark members as static - mimmicking ICollection<Vector>
    public bool IsReadOnly => false;
#pragma warning restore CA1822 // Mark members as static - mimmicking ICollection<Vector>

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _dataFile.Dispose();
                _indexFile.Dispose();
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
        lock (_mutex)
        {
            if (index < 0L || index >= _count)
            {
                return null;
            }

            _indexFile.Stream.Seek(index * s_indexEntryByteLength, SeekOrigin.Begin);
            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            _indexFile.Stream.ReadExactly(entry);

            Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
            Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
            long offset = BitConverter.ToInt64(offsetBytes);
            int length = BitConverter.ToInt32(lengthBytes);

            _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
            Span<byte> bytes = stackalloc byte[length];
            _dataFile.Stream.ReadExactly(bytes);
            return new Vector(bytes);
        }
    }

    public Vector? GetVector(Guid id)
    {
        lock (_mutex)
        {
            (_, long offset, int length) = SearchVectorInIndex(id);
            if (offset < 0L || length < 0L)
            {
                return null;
            }

            _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
            Span<byte> bytes = stackalloc byte[length];
            _dataFile.Stream.ReadExactly(bytes);
            return new Vector(bytes);
        }
    }

    public void CopyTo(Vector[] array, int arrayIndex)
    {
        lock (_mutex)
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

            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }
    }

    public long FindIndexById(Guid id)
    {
        lock (_mutex)
        {
            (long index, _, _) = SearchVectorInIndex(id);
            return index;
        }
    }

    public long IndexOf(Vector item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_mutex)
        {
            (long index, _, _) = SearchVectorInIndex(item.Id);
            return index;
        }
    }

    public void Add(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        lock (_mutex)
        {
            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
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

            ++_count;
        }
    }

    public bool Remove(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        lock (_mutex)
        {
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
                    // Seek to the beginning of the entry and remove the ID with a tombstone-ID
                    _indexFile.Stream.Seek(-s_indexEntryByteLength, SeekOrigin.Current);
                    _indexFile.Stream.Write(s_tombStoneBytes);
                    // Seek to after the entry
                    _indexFile.Stream.Seek(s_indexEntryByteLength - s_idBytesLength, SeekOrigin.Current);
                    --_count;
                    return true;
                }
                else if (id.Equals(Guid.Empty))
                {
                    break;
                }
            }
        }

        return false;
    }

    public bool Update(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        lock (_mutex)
        {
            if (Remove(vector))
            {
                Add(vector);
                return true;
            }

            return false;
        }
    }

    public void Defrag()
    {
        // TODO: Implement defragmentation - remove tombstones and compact data
        throw new NotImplementedException();
    }

    public void Clear()
    {
        lock (_mutex)
        {
            _indexFile.Reset();
            _dataFile.Reset();
            _count = 0;
        }
    }

    public bool Contains(Vector item)
    {
        if (item is null)
        {
            return false;
        }

        var vector = GetVector(item.Id);
        if (vector is null)
        {
            return false;
        }

        return vector.Equals(item);
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        lock (_mutex)
        {
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

                if (id.Equals(Guid.Empty))
                {
                    break;
                }

                Span<byte> offsetBytes = entrySpan[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                Span<byte> lengthBytes = entrySpan[(s_idBytesLength + s_offsetBytesLength)..];
                long offset = BitConverter.ToInt64(offsetBytes);
                int length = BitConverter.ToInt32(lengthBytes);

                _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
                Span<byte> bytes = stackalloc byte[length];
                _dataFile.Stream.ReadExactly(bytes);
                yield return new Vector(bytes);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private (long index, long offset, int length) SearchVectorInIndex(Guid id)
    {
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
                break;
            }

            ++index;
        }

        return (-1L, -1L, -1);
    }

    private class MemoryMappedFileHolder : IDisposable
    {
        private readonly long _capacity;
        private MemoryMappedFile _file;
        private MemoryMappedViewStream _stream;
        private bool _disposedValue;
        private string _fileName;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Reset()
        public MemoryMappedFileHolder(long capacity)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Reset()
        {
            _capacity = capacity;

            Reset();
        }

        public MemoryMappedViewStream Stream => _stream;

        public void Reset()
        {
            DisposeStreams();

            _fileName= Path.GetTempFileName();
            Logging.Logger.Information("Creating temporary file: {FileName}, size {capacity} GiB", _fileName, _capacity/1024/1024);
            try
            {
                _file = MemoryMappedFile.CreateFromFile(_fileName, FileMode.OpenOrCreate, null, _capacity);
                _stream = _file.CreateViewStream();
            }
            catch (System.IO.IOException ex)
            {
                Logging.Logger.Error(ex, "Failed to create memory-mapped file");
                if (File.Exists(_fileName))
                {
                    File.Delete(_fileName);
                    Logging.Logger.Information($"File deleted ({_fileName}) due to error: {ex.Message}");
                }
                throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DisposeStreams();
                    try
                    {
                        if (File.Exists(_fileName))
                        {
                            File.Delete(_fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Error(ex, "Failed to delete temporary file: {FileName}", _fileName);
                    }
                }

                _disposedValue = true;
            }
        }

        ~MemoryMappedFileHolder()
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

        private void DisposeStreams()
        {
            _stream?.Dispose();
            _file?.Dispose();
        }
    }
}