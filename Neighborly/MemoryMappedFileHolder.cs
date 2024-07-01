using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly;


internal class MemoryMappedFileHolder : IDisposable
{
    private readonly long _capacity;
    private MemoryMappedFile _file;
    private MemoryMappedViewStream _stream;
    private bool _disposedValue;
    private string _fileName;
    public string Filename
    {
        get { return _fileName; }
    }
    public long Capacity
    {
        get { return _capacity; }
    }

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
        _fileName = Path.GetTempFileName();
        MemoryMappedFileServices.WinFileAlloc(_fileName);
        double capacityTiB = _capacity / (1024.0 * 1024.0 * 1024.0 * 1024.0);
        Logging.Logger.Information("Creating temporary file: {FileName}, size {capacity} TiB", _fileName, capacityTiB);
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

    public void DisposeStreams()
    {
        _stream?.Dispose();
        _file?.Dispose();
    }
}
