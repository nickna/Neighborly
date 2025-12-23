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
    private readonly List<RandomAccessFileHolder> _randomAccessFiles = new();
    private bool _disposedValue;

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

    public void RegisterFile(RandomAccessFileHolder file)
    {
        _randomAccessFiles.Add(file);
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
        foreach (var file in _randomAccessFiles)
        {
            try
            {
                file.FlushToDisk();
            }
            catch (Exception ex)
            {
                Logging.Logger.Warning(ex, "Failed to flush file: {FileName}", file.Filename);
            }
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