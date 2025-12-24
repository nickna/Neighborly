using Microsoft.Win32.SafeHandles;
using System.Buffers;

namespace Neighborly;

/// <summary>
/// Manages a file handle for position-independent I/O using System.IO.RandomAccess.
/// This replaces MemoryMappedFileHolder with a simpler, more efficient implementation.
/// Supports dynamic file growth based on configurable growth strategy.
/// </summary>
internal class RandomAccessFileHolder : IDisposable
{
    private long _currentCapacity;  // Current mutable capacity
    private readonly long _capacity;  // Original capacity (for backward compat)
    private SafeFileHandle? _handle;
    private bool _disposedValue;
    private string _fileName;
    private readonly FileGrowthStrategy? _growthStrategy;  // null = no growth
    private readonly Lock _resizeLock = new();  // Protect resize operations

    public string Filename => _fileName;
    public long Capacity => _currentCapacity;  // Return current capacity

    /// <summary>
    /// Creates a file holder with dynamic growth capability.
    /// </summary>
    /// <param name="growthStrategy">The growth strategy to use for automatic file expansion.</param>
    public RandomAccessFileHolder(FileGrowthStrategy growthStrategy)
    {
        ArgumentNullException.ThrowIfNull(growthStrategy);
        _growthStrategy = growthStrategy;
        _currentCapacity = growthStrategy.InitialCapacity;
        _capacity = _currentCapacity;
        _fileName = string.Empty;
        Reset();
    }

    /// <summary>
    /// Creates a file holder with fixed capacity (no automatic growth).
    /// </summary>
    /// <param name="capacity">The fixed capacity in bytes.</param>
    public RandomAccessFileHolder(long capacity)
    {
        _capacity = capacity;
        _currentCapacity = capacity;
        _growthStrategy = null;  // No automatic growth
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

        double capacityTiB = _currentCapacity / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        Logging.Logger.Information("Creating temporary file: {FileName}, size {capacity} TiB", _fileName, capacityTiB);

        try
        {
            _handle = File.OpenHandle(
                _fileName,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.Read,
                FileOptions.RandomAccess,
                _currentCapacity);

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

    /// <summary>
    /// Checks if the file should grow and calculates new capacity if needed.
    /// Returns null if no growth needed, otherwise returns recommended new capacity.
    /// </summary>
    /// <param name="requiredSpace">The space required for an upcoming operation.</param>
    /// <returns>New capacity if growth is needed, null otherwise.</returns>
    public long? CheckAndCalculateGrowth(long requiredSpace)
    {
        if (_growthStrategy == null)
            return null; // Growth disabled

        long currentLength = GetLength();

        // Check if we need to grow to accommodate this operation
        if (requiredSpace > _currentCapacity)
        {
            // Must grow - insufficient capacity
            return _growthStrategy.CalculateNewCapacity(_currentCapacity, requiredSpace);
        }

        // Check threshold-based growth (proactive)
        if (_growthStrategy.ShouldGrow(_currentCapacity, currentLength))
        {
            return _growthStrategy.CalculateNewCapacity(_currentCapacity, requiredSpace);
        }

        return null; // No growth needed
    }

    /// <summary>
    /// Resizes the file to new capacity using atomic temp file + rename pattern.
    /// Thread-safe. Caller should hold write lock in MemoryMappedList.
    /// </summary>
    /// <param name="newCapacity">Target capacity (must be > current).</param>
    public void ResizeFile(long newCapacity)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));

        if (newCapacity <= _currentCapacity)
            throw new ArgumentException($"New capacity ({newCapacity}) must be greater than current ({_currentCapacity})", nameof(newCapacity));

        using (_resizeLock.EnterScope())
        {
            ResizeFileInternal(newCapacity);
        }
    }

    /// <summary>
    /// Asynchronously resizes file by offloading to thread pool.
    /// Note: The resize operation itself uses synchronous I/O,
    /// but this prevents blocking async callers.
    /// </summary>
    public ValueTask ResizeFileAsync(long newCapacity, CancellationToken cancellationToken = default)
    {
        if (_handle == null)
            throw new ObjectDisposedException(nameof(RandomAccessFileHolder));

        // Offload to thread pool to avoid blocking async context
        return new ValueTask(Task.Run(() => ResizeFile(newCapacity), cancellationToken));
    }

    private void ResizeFileInternal(long newCapacity)
    {
        string tempFileName = Path.GetTempFileName();
        SafeFileHandle? tempHandle = null;

        try
        {
            // Step 1: Create temp file with new capacity
            MemoryMappedFileServices.WinFileAlloc(tempFileName);

            tempHandle = File.OpenHandle(
                tempFileName,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                FileOptions.RandomAccess,
                newCapacity);

            Logging.Logger.Information(
                "Creating temp file for resize: {TempFile}, new capacity {NewCapacity} bytes",
                tempFileName, newCapacity);

            // Step 2: Copy existing data
            long currentLength = GetLength();
            if (currentLength > 0)
            {
                CopyFileFast(_handle!, tempHandle, currentLength);
            }

            // Step 3: Flush temp file to disk (durability)
            RandomAccess.FlushToDisk(tempHandle);

            // Step 4: Atomic swap
            string oldFileName = _fileName;
            SafeFileHandle oldHandle = _handle!;

            // Close old handle
            oldHandle.Dispose();

            // Delete old file
            if (File.Exists(oldFileName))
            {
                File.Delete(oldFileName);
            }

            // Rename temp to old location
            File.Move(tempFileName, oldFileName, overwrite: true);

            // Update state
            _handle = tempHandle;
            tempHandle = null; // Prevent disposal in finally
            _currentCapacity = newCapacity;

            Logging.Logger.Information(
                "Successfully resized file {FileName} to {NewCapacity} bytes",
                _fileName, newCapacity);
        }
        catch (Exception ex)
        {
            Logging.Logger.Error(ex, "Failed to resize file {FileName} to {NewCapacity}", _fileName, newCapacity);

            // Cleanup temp file on failure
            tempHandle?.Dispose();
            if (File.Exists(tempFileName))
            {
                try { File.Delete(tempFileName); }
                catch { /* Best effort cleanup */ }
            }
            throw;
        }
    }

    /// <summary>
    /// Fast file copy using ArrayPool buffer to minimize GC pressure.
    /// </summary>
    private void CopyFileFast(SafeFileHandle source, SafeFileHandle dest, long length)
    {
        const int bufferSize = 1024 * 1024; // 1 MB buffer
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            long offset = 0;
            while (offset < length)
            {
                int toRead = (int)Math.Min(bufferSize, length - offset);
                int read = RandomAccess.Read(source, buffer.AsSpan(0, toRead), offset);
                if (read == 0)
                    throw new IOException($"Unexpected end of file at offset {offset}");

                RandomAccess.Write(dest, buffer.AsSpan(0, read), offset);
                offset += read;
            }

            Logging.Logger.Debug("Copied {Length} bytes during file resize", length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

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
