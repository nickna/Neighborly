using Microsoft.Win32.SafeHandles;

namespace Neighborly;

/// <summary>
/// Manages a file handle for position-independent I/O using System.IO.RandomAccess.
/// This replaces MemoryMappedFileHolder with a simpler, more efficient implementation.
/// </summary>
internal class RandomAccessFileHolder : IDisposable
{
    private readonly long _capacity;
    private SafeFileHandle? _handle;
    private bool _disposedValue;
    private string _fileName;

    public string Filename => _fileName;
    public long Capacity => _capacity;

    public RandomAccessFileHolder(long capacity)
    {
        _capacity = capacity;
        _fileName = string.Empty;
        Reset();
    }

    /// <summary>
    /// Creates a new temporary file and opens a handle for random access I/O.
    /// </summary>
    public void Reset()
    {
        _fileName = Path.GetTempFileName();
        MemoryMappedFileServices.WinFileAlloc(_fileName);

        double capacityTiB = _capacity / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        Logging.Logger.Information("Creating temporary file: {FileName}, size {capacity} TiB", _fileName, capacityTiB);

        try
        {
            _handle = File.OpenHandle(
                _fileName,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read,
                FileOptions.RandomAccess,
                _capacity);

            // Apply SSD optimizations
            SSDOptimizer.OptimizeFileForSSD(_fileName);
        }
        catch (IOException ex)
        {
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
                Logging.Logger.Error("Error occurred while trying to create file ({FileName}). File was successfully deleted. Error: {Message}", _fileName, ex.Message);
            }
            else
            {
                Logging.Logger.Error("Error occurred while trying to create file ({FileName}). Error: {Message}", _fileName, ex.Message);
            }
            throw;
        }
    }

    /// <summary>
    /// Reads data at the specified position. Thread-safe for concurrent reads.
    /// </summary>
    public int Read(long offset, Span<byte> buffer)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        return RandomAccess.Read(_handle, buffer, offset);
    }

    /// <summary>
    /// Reads exactly the requested number of bytes at the specified position.
    /// Throws if fewer bytes are available.
    /// </summary>
    public void ReadExactly(long offset, Span<byte> buffer)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));

        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = RandomAccess.Read(_handle, buffer[totalRead..], offset + totalRead);
            if (read == 0)
                throw new EndOfStreamException($"Unable to read {buffer.Length} bytes at offset {offset}");
            totalRead += read;
        }
    }

    /// <summary>
    /// Writes data at the specified position. Thread-safe for non-overlapping writes.
    /// </summary>
    public void Write(long offset, ReadOnlySpan<byte> buffer)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        RandomAccess.Write(_handle, buffer, offset);
    }

    /// <summary>
    /// Asynchronously reads data at the specified position.
    /// </summary>
    public ValueTask<int> ReadAsync(long offset, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        return RandomAccess.ReadAsync(_handle, buffer, offset, cancellationToken);
    }

    /// <summary>
    /// Asynchronously writes data at the specified position.
    /// </summary>
    public ValueTask WriteAsync(long offset, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        return RandomAccess.WriteAsync(_handle, buffer, offset, cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads exactly the requested number of bytes at the specified position.
    /// Throws if fewer bytes are available.
    /// </summary>
    public async ValueTask ReadExactlyAsync(long offset, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));

        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await RandomAccess.ReadAsync(_handle, buffer[totalRead..], offset + totalRead, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException($"Unable to read {buffer.Length} bytes at offset {offset}");
            totalRead += read;
        }
    }

    /// <summary>
    /// Gets the current length of the file.
    /// </summary>
    public long GetLength()
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        return RandomAccess.GetLength(_handle);
    }

    /// <summary>
    /// Sets the length of the file.
    /// </summary>
    public void SetLength(long length)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        RandomAccess.SetLength(_handle, length);
    }

    /// <summary>
    /// Flushes all file buffers to disk, ensuring data durability.
    /// </summary>
    public void FlushToDisk()
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        RandomAccess.FlushToDisk(_handle);
    }

    /// <summary>
    /// Asynchronously flushes all file buffers to disk, ensuring data durability.
    /// Note: RandomAccess.FlushToDisk is synchronous, so this offloads to the thread pool.
    /// </summary>
    public ValueTask FlushToDiskAsync(CancellationToken cancellationToken = default)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));
        return new ValueTask(Task.Run(() => RandomAccess.FlushToDisk(_handle), cancellationToken));
    }

    /// <summary>
    /// Gets the starting position for data in the file.
    /// </summary>
    public long GetDataStartPosition() => 0;

    /// <summary>
    /// Gets the underlying SafeFileHandle for platform-specific operations.
    /// </summary>
    internal SafeFileHandle? Handle => _handle;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                DisposeHandle();
                try
                {
                    if (File.Exists(_fileName))
                    {
                        File.Delete(_fileName);
                        Logging.Logger.Information("Deleted temporary file: {FileName}", _fileName);
                    }
                    else
                    {
                        Logging.Logger.Warning("Temporary file not found: {FileName}", _fileName);
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

    ~RandomAccessFileHolder()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes only the file handle without deleting the file.
    /// Used for file reset operations.
    /// </summary>
    public void DisposeHandle()
    {
        _handle?.Dispose();
        _handle = null;
    }
}
