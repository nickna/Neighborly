namespace Neighborly;

/// <summary>
/// Manages checkpoint operations for WAL-based durability.
/// Coordinates data file flushing with WAL truncation to ensure ACID guarantees.
/// </summary>
internal class CheckpointManager : IDisposable
{
    private readonly FlushPolicy _policy;
    private readonly int _batchSize;
    private readonly int _timerInterval;
    private readonly Timer? _checkpointTimer;
    private readonly List<RandomAccessFileHolder> _dataFiles = new();
    private readonly WriteAheadLog _wal;

    private readonly Lock _lock = new();
    private int _unflushedOperationCount;
    private long _lastUnflushedLSN;
    private long _firstUnflushedLSN;
    private bool _disposedValue;

    /// <summary>
    /// Creates a new CheckpointManager that coordinates durability between data files and WAL.
    /// </summary>
    /// <param name="wal">The WriteAheadLog to checkpoint.</param>
    /// <param name="policy">The flush policy to use.</param>
    /// <param name="batchSize">Number of operations before automatic checkpoint (for Batched policy).</param>
    /// <param name="timerInterval">Milliseconds between checkpoints (for Timer policy).</param>
    public CheckpointManager(
        WriteAheadLog wal,
        FlushPolicy policy = FlushPolicy.Batched,
        int batchSize = 100,
        int timerInterval = 5000)
    {
        _wal = wal ?? throw new ArgumentNullException(nameof(wal));
        _policy = policy;
        _batchSize = batchSize;
        _timerInterval = timerInterval;
        _firstUnflushedLSN = 0;
        _lastUnflushedLSN = 0;

        if (_policy == FlushPolicy.Timer)
        {
            _checkpointTimer = new Timer(TimerCheckpoint, null, _timerInterval, _timerInterval);
        }
    }

    /// <summary>
    /// Registers a data file to be flushed during checkpoints.
    /// </summary>
    public void RegisterFile(RandomAccessFileHolder file)
    {
        using (_lock.EnterScope())
        {
            _dataFiles.Add(file);
        }
    }

    /// <summary>
    /// Records an operation with its LSN for checkpoint tracking.
    /// May trigger an immediate checkpoint based on FlushPolicy.
    /// </summary>
    /// <param name="lsn">The LSN returned from WriteAheadLog.LogOperation()</param>
    public void RecordOperation(long lsn)
    {
        using (_lock.EnterScope())
        {
            if (_firstUnflushedLSN == 0)
            {
                _firstUnflushedLSN = lsn;
            }
            _lastUnflushedLSN = lsn;
            _unflushedOperationCount++;

            switch (_policy)
            {
                case FlushPolicy.Immediate:
                    PerformCheckpointInternal();
                    break;

                case FlushPolicy.Batched when _unflushedOperationCount >= _batchSize:
                    PerformCheckpointInternal();
                    break;

                case FlushPolicy.None:
                case FlushPolicy.Timer:
                    // No immediate action
                    break;
            }
        }
    }

    /// <summary>
    /// Forces a checkpoint: flushes all data files to disk, then truncates WAL.
    /// </summary>
    public void ForceCheckpoint()
    {
        using (_lock.EnterScope())
        {
            PerformCheckpointInternal();
        }
    }

    private void PerformCheckpointInternal()
    {
        if (_unflushedOperationCount == 0 || _lastUnflushedLSN == 0)
            return;

        // Step 1: Flush all data files to disk
        foreach (var file in _dataFiles)
        {
            try
            {
                file.FlushToDisk();
            }
            catch (Exception ex)
            {
                Logging.Logger.Warning(ex, "Failed to flush file during checkpoint: {FileName}", file.Filename);
                // If any file fails to flush, don't checkpoint WAL
                // This maintains the invariant that WAL is only cleared after confirmed flush
                return;
            }
        }

        // Step 2: Only after successful flush, checkpoint WAL up to last LSN
        long checkpointLSN = _lastUnflushedLSN;
        _wal.Checkpoint(checkpointLSN);

        Logging.Logger.Debug("Checkpoint completed up to LSN {LSN}", checkpointLSN);

        // Reset tracking
        _unflushedOperationCount = 0;
        _firstUnflushedLSN = 0;
        _lastUnflushedLSN = 0;
    }

    private void TimerCheckpoint(object? state)
    {
        using (_lock.EnterScope())
        {
            if (_unflushedOperationCount > 0)
            {
                PerformCheckpointInternal();
            }
        }
    }

    /// <summary>
    /// Returns the number of operations not yet checkpointed.
    /// </summary>
    public int UnflushedOperationCount
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _unflushedOperationCount;
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _checkpointTimer?.Dispose();

                // Final checkpoint before disposal to ensure all data is flushed
                using (_lock.EnterScope())
                {
                    PerformCheckpointInternal();
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
