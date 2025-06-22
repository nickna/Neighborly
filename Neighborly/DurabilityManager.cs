using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Neighborly;

public enum FlushPolicy
{
    None,           // No automatic flushing
    Immediate,      // Flush after every write
    Batched,        // Flush after N operations
    Timer           // Flush every N milliseconds
}

internal class DurabilityManager : IDisposable
{
    private readonly FlushPolicy _policy;
    private readonly int _batchSize;
    private readonly int _timerInterval;
    private readonly Timer? _flushTimer;
    private int _operationCount;
    private readonly List<MemoryMappedFileHolder> _files = new();
    private bool _disposedValue;

    // Platform-specific P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    [DllImport("libc", SetLastError = true)]
    private static extern int fsync(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int fdatasync(int fd);

    public DurabilityManager(FlushPolicy policy = FlushPolicy.Batched, int batchSize = 100, int timerInterval = 5000)
    {
        _policy = policy;
        _batchSize = batchSize;
        _timerInterval = timerInterval;

        if (_policy == FlushPolicy.Timer)
        {
            _flushTimer = new Timer(TimerFlush, null, _timerInterval, _timerInterval);
        }
    }

    public void RegisterFile(MemoryMappedFileHolder file)
    {
        _files.Add(file);
    }

    public void RecordOperation()
    {
        Interlocked.Increment(ref _operationCount);

        switch (_policy)
        {
            case FlushPolicy.Immediate:
                ForceFlush();
                break;
            case FlushPolicy.Batched when _operationCount >= _batchSize:
                ForceFlush();
                Interlocked.Exchange(ref _operationCount, 0);
                break;
        }
    }

    public void ForceFlush()
    {
        foreach (var file in _files)
        {
            try
            {
                file.Stream.Flush();
                PlatformSpecificSync(file);
            }
            catch (Exception ex)
            {
                Logging.Logger.Warning(ex, "Failed to flush file: {FileName}", file.Filename);
            }
        }
    }

    private void PlatformSpecificSync(MemoryMappedFileHolder file)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use FlushFileBuffers on Windows
                using var fileStream = new FileStream(file.Filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                FlushFileBuffers(fileStream.SafeFileHandle);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                     RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Use fsync on Unix-like systems
                using var fileStream = new FileStream(file.Filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                fsync(fileStream.SafeFileHandle.DangerousGetHandle().ToInt32());
            }
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Platform-specific sync failed for file: {FileName}", file.Filename);
        }
    }

    private void TimerFlush(object? state)
    {
        if (_operationCount > 0)
        {
            ForceFlush();
            Interlocked.Exchange(ref _operationCount, 0);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _flushTimer?.Dispose();
                ForceFlush(); // Final flush before disposal
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