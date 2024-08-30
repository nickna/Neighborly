using Parquet.Schema;
using Serilog.Core;
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
    private NeighborlyFileMode _fileMode;
    private MemoryMappedFileServices.FilePurpose _filePurpose;

    public bool SaveOnDispose { get; set; } = true;

    /// <summary>
    /// Gets the name of the temporary file backing the memory-mapped file.
    /// </summary>
    public string Filename
    {
        get { return _fileName; }
    }

    private bool _fileExists => !string.IsNullOrEmpty(_fileName) && File.Exists(_fileName);
    // this will be true if the file was created by Path.GetTempFileName()
    // this should also be true if the file was previously created by this class


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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Start()
    public MemoryMappedFileHolder(
        MemoryMappedFileServices.FilePurpose filePurpose, 
        long capacityInBytes, string? 
        fileName = null, 
        NeighborlyFileMode fileMode = NeighborlyFileMode.OpenOrCreate
        )
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Start()
    {
        _capacity = capacityInBytes;
        _fileName = fileName ?? string.Empty;
        _fileMode = fileMode;
        _filePurpose = filePurpose;
        Start();
    }

    /// <summary>
    /// Gets the stream to access the memory-mapped file.
    /// </summary>
    public MemoryMappedViewStream Stream => _stream;


    /// <summary>
    /// Destroys the memory-mapped file and creates a new one.
    /// </summary>
    public void Reset()
    {
        Logging.Logger.Information("MMFH.Reset: Resetting memory-mapped file: {FileName}", _fileName);
        DisposeStreams();

        if ( _fileMode != NeighborlyFileMode.InMemory)
        {
            MemoryMappedFileServices.DeleteFile(_fileName);
            Logging.Logger.Information("MMFH.Reset: Deleted memory-mapped file: {FileName}", _fileName);
        }

        Start();
    }

    /// <summary>
    /// Prepares the memory-mapped file, creating a new temporary file and stream.
    /// </summary>
    /// <param name="fileName">The name of the temporary file to create. If null or empty, a new temporary file is created.</param>
    /// <param name="fileMode">The file mode to use when creating the file.</param>
    public void Start()
    {
        bool newlyCreatedFile = false;

        // Create a temporary file if we are going to work with files 
        // and a file was not provided.
        if (_fileMode != NeighborlyFileMode.InMemory)
            _fileName = string.IsNullOrEmpty(_fileName) ? Path.GetTempFileName() : _fileName;
        else
            _fileName = "InMemory"; // Use a dummy file name for in-memory files

        if (_fileExists && // If the file exists and we are creating a new file, delete the existing file
            _fileMode == NeighborlyFileMode.CreateNew)
        {
            MemoryMappedFileServices.DeleteFile(_fileName);
            Logging.Logger.Information("MMFH.Start: Deleted, recreating {FilePurpose} file: {FileName}", _filePurpose,  _fileName);
        }
        else if (_fileExists && _fileMode == NeighborlyFileMode.OpenOrCreate)
        {
            // If the file exists and we are opening it, do nothing
            Logging.Logger.Information("MMFH.Start: Using existing {FilePurpose} file: {FileName}", _filePurpose, _fileName);
        }
        else if (!_fileExists && _fileMode == NeighborlyFileMode.OpenOrCreate)
        {
            newlyCreatedFile = true;
            // If the file does not exist and we are opening it, create it
            File.Create(_fileName).Dispose();
            Logging.Logger.Information("MMFH.Start: Created new {FilePurpose} file {FileName}", _filePurpose, _fileName);
            Thread.Sleep(500);  // Wait for the file to be created. This is necessary because the file may not be immediately available
                                // TODO: Find a better way to handle this
            MemoryMappedFileServices.WinFileAlloc(_fileName);
        }
        else if (_fileMode == NeighborlyFileMode.InMemory)
        {
            Logging.Logger.Information("MMFH.Start: Creating in-memory {FilePurpose} file", _filePurpose);
        }
        
        double capacityTiB = _capacity / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        try
        {
            switch (_fileMode)
            {
                case NeighborlyFileMode.InMemory:
                    _file = MemoryMappedFile.CreateNew(null, _capacity);
                    break;
                case NeighborlyFileMode.CreateNew:
                    MemoryMappedFileServices.DeleteFile(_fileName); // Delete the file if it exists (this should not happen, but just in case
                    _file = MemoryMappedFile.CreateFromFile(_fileName, FileMode.CreateNew, null, _capacity);
                    break;
                case NeighborlyFileMode.OpenOrCreate:
                    _file = MemoryMappedFile.CreateFromFile(_fileName, FileMode.OpenOrCreate, null, _capacity);
                    break;
                default:
                    throw new ArgumentException("MMFH.Start: Invalid file mode", nameof(_fileMode));
            }
            _stream = _file.CreateViewStream();
            // Logging.Logger.Information("MMFH.Start: Opened file {FileName}, size {capacityInBytes} TiB", _fileName, capacityTiB);
        }
        catch (System.IO.IOException ex)
        {
            if (newlyCreatedFile && File.Exists(_fileName)) // If the file was created and then failed to open, delete it
            {
                _stream?.Dispose();
                _file?.Dispose();
                MemoryMappedFileServices.DeleteFile(_fileName);
                Logging.Logger.Error($"MMFH.Start: Error occurred while trying to use {_filePurpose} file ({_fileName}). File was successfully deleted. Error: {ex.Message}");
            }
            else // If we failed to open the existing file, log the error
            {
                Logging.Logger.Error($"MMFH.Start: Error occurred while trying to open {_filePurpose} file ({_fileName}). Error: {ex.Message}");
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
                if (!SaveOnDispose)
                {
                    try
                    {
                        if (File.Exists(_fileName))
                        {
                            MemoryMappedFileServices.DeleteFile(_fileName);
                            Logging.Logger.Information("MMFH.Dispose: Deleted temporary file {FileName}", _fileName);
                        }
                        else
                        {
                            Logging.Logger.Warning("MMFH.Dispose: Temporary file not found {FileName}", _fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Error(ex, "MMFH.Dispose: Failed to delete temporary file {FileName}", _fileName);
                    }
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
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
