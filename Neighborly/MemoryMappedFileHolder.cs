using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly;

/// <summary>
/// Manages a memory-mapped file, providing a stream to access its contents.
/// </summary>
internal class MemoryMappedFileHolder : IDisposable
{
    private readonly long _capacity;
    private MemoryMappedFile _file;
    private MemoryMappedViewStream _stream;
    private bool _disposedValue;
    private string _fileName;

    /// <summary>
    /// Gets the name of the temporary file backing the memory-mapped file.
    /// </summary>
    public string Filename
    {
        get { return _fileName; }
    }

    /// <summary>
    /// Gets the capacityInBytes of the memory-mapped file, in bytes.
    /// </summary>
    public long Capacity
    {
        get { return _capacity; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryMappedFileHolder"/> class with the specified capacityInBytes.
    /// </summary>
    /// <param name="capacityInBytes">The capacityInBytes (in bytes) of the memory-mapped file.</param>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Reset()
    public MemoryMappedFileHolder(long capacityInBytes)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Reset()
    {
        _capacity = capacityInBytes;

        Reset();
    }

    /// <summary>
    /// Gets the stream to access the memory-mapped file.
    /// </summary>
    public MemoryMappedViewStream Stream => _stream;

    /// <summary>
    /// Resets the memory-mapped file, creating a new temporary file and stream.
    /// </summary>
    public void Reset()
    {
        _fileName = Path.GetTempFileName();
        MemoryMappedFileServices.WinFileAlloc(_fileName);
        double capacityTiB = _capacity / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        Logging.Logger.Information("Creating temporary file: {FileName}, size {capacityInBytes} TiB", _fileName, capacityTiB);
        try
        {
            _file = MemoryMappedFile.CreateFromFile(_fileName, FileMode.OpenOrCreate, null, _capacity);
            _stream = _file.CreateViewStream();
        }
        catch (System.IO.IOException ex)
        {
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
                Logging.Logger.Error($"Error occurred while trying to create file ({_fileName}). File was successfully deleted. Error: {ex.Message}");
            }
            else
            {
                Logging.Logger.Error($"Error occurred while trying to create file ({_fileName}). Error: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="MemoryMappedFileHolder"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
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

    /// <summary>
    /// Finalizes an instance of the <see cref="MemoryMappedFileHolder"/> class.
    /// </summary>
    ~MemoryMappedFileHolder()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the memory-mapped file and its associated stream.
    /// </summary>
    public void DisposeStreams()
    {
        _stream?.Dispose();
        _file?.Dispose();
    }
}
