using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Neighborly;

/// <summary>
/// Delegate for the callback function that performs index rebuilding.
/// </summary>
/// <param name="cancellationToken">Cancellation token for the operation</param>
/// <returns>A task representing the asynchronous operation</returns>
public delegate Task IndexRebuildCallback(CancellationToken cancellationToken);

/// <summary>
/// Background service that periodically rebuilds search indexes when the database is modified.
/// Implements debouncing to batch multiple rapid changes into a single rebuild operation.
/// </summary>
public sealed class BackgroundIndexService : IDisposable, IAsyncDisposable
{
    private readonly IndexRebuildCallback _rebuildCallback;
    private readonly BackgroundIndexServiceOptions _options;
    private readonly ILogger<BackgroundIndexService> _logger;
    private readonly Instrumentation _instrumentation;

    // Background task management
    private Task? _indexingTask;
    private PeriodicTimer? _indexingTimer;
    private readonly CancellationTokenSource _shutdownCts = new();

    // State tracking
    private DateTime _lastModification = DateTime.UtcNow;
    private bool _hasOutdatedIndex = false;
    private bool _disposedValue;
    private int _asyncDisposeStarted;

    // Metrics
    private readonly System.Diagnostics.Metrics.Counter<long> _workerRunsCounter;
    private readonly System.Diagnostics.Metrics.Counter<long> _workerErrorsCounter;

    /// <summary>
    /// Creates a new background index service.
    /// </summary>
    /// <param name="rebuildCallback">Callback function to invoke when indexes need rebuilding</param>
    /// <param name="options">Configuration options for the service</param>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="instrumentation">Optional instrumentation for metrics and tracing</param>
    public BackgroundIndexService(
        IndexRebuildCallback rebuildCallback,
        BackgroundIndexServiceOptions options,
        ILogger<BackgroundIndexService> logger,
        Instrumentation? instrumentation = null)
    {
        ArgumentNullException.ThrowIfNull(rebuildCallback);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _rebuildCallback = rebuildCallback;
        _options = options;
        _logger = logger;
        _instrumentation = instrumentation ?? Instrumentation.Instance;

        // Initialize metrics
        _workerRunsCounter = _instrumentation.Meter.CreateCounter<long>(
            name: "neighborly.index.service.runs",
            unit: "{runs}",
            description: "The number of times the background indexing worker has checked for rebuilds.");

        _workerErrorsCounter = _instrumentation.Meter.CreateCounter<long>(
            name: "neighborly.index.service.errors",
            unit: "{errors}",
            description: "The number of errors encountered by the background indexing worker.");
    }

    /// <summary>
    /// Notifies the service that the database has been modified.
    /// This sets flags that trigger a rebuild after the configured delay.
    /// </summary>
    public void NotifyModified()
    {
        _lastModification = DateTime.UtcNow;
        _hasOutdatedIndex = true;
    }

    /// <summary>
    /// Starts the background indexing service if enabled.
    /// </summary>
    public void Start()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Background indexing service is disabled by configuration.");
            return;
        }

        if (_indexingTask != null)
        {
            _logger.LogWarning("Background indexing service is already running.");
            return;
        }

        // Start the async background task on the thread pool
        _indexingTask = Task.Run(
            () => IndexingWorkerAsync(_shutdownCts.Token),
            _shutdownCts.Token);

        _logger.LogDebug(
            "Background indexing service started (RebuildDelay={RebuildDelay}s, CheckInterval={CheckInterval}s).",
            _options.RebuildDelay.TotalSeconds,
            _options.CheckInterval.TotalSeconds);
    }

    /// <summary>
    /// Triggers an immediate index rebuild, bypassing the configured delay.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task TriggerRebuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await PerformIndexRebuildAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual index rebuild trigger.");
            throw;
        }
    }

    /// <summary>
    /// Async background worker for periodic index rebuilding.
    /// Uses PeriodicTimer for cancellation-aware, async-native timed loops.
    /// </summary>
    private async Task IndexingWorkerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background indexing task started.");

        try
        {
            // Create PeriodicTimer with configured check interval
            _indexingTimer = new PeriodicTimer(_options.CheckInterval);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for next tick - this is cancellation-aware
                    if (!await _indexingTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    {
                        // Timer was disposed, exit loop
                        break;
                    }

                    _workerRunsCounter.Add(1);

                    // Check if rebuild is needed
                    if (_hasOutdatedIndex &&
                        DateTime.UtcNow.Subtract(_lastModification) > _options.RebuildDelay)
                    {
                        await PerformIndexRebuildAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected during shutdown, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue running - resilient to transient failures
                    _logger.LogError(ex, "Error during background indexing worker iteration.");
                    _workerErrorsCounter.Add(1);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background indexing task cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in background indexing task.");
            _workerErrorsCounter.Add(1);
        }
        finally
        {
            _logger.LogInformation("Background indexing task stopped.");
        }
    }

    /// <summary>
    /// Performs the actual index rebuild operation by invoking the callback.
    /// </summary>
    private async Task PerformIndexRebuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rebuildCallback(cancellationToken).ConfigureAwait(false);
            _hasOutdatedIndex = false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Index rebuild cancelled during operation.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during background index rebuild.");
            // Don't rethrow - allow the worker to continue
        }
    }

    /// <summary>
    /// Stops the background indexing service gracefully.
    /// Guarantees completion within the specified timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown. If null, uses configured ShutdownTimeout.</param>
    /// <returns>True if shutdown completed gracefully, false if timed out.</returns>
    public async Task<bool> StopAsync(TimeSpan? timeout = null)
    {
        timeout ??= _options.ShutdownTimeout;

        _logger.LogInformation("Stopping background indexing service...");

        // Dispose the timer first to unblock WaitForNextTickAsync
        _indexingTimer?.Dispose();
        _indexingTimer = null;

        if (_indexingTask is null || _indexingTask.IsCompleted)
        {
            _logger.LogDebug("Indexing task already completed or was never started.");
            return true;
        }

        try
        {
            // Signal cancellation using async cancel (.NET 8+)
            await _shutdownCts.CancelAsync().ConfigureAwait(false);

            // Wait for task with timeout using Task.WaitAsync (non-blocking)
            await _indexingTask.WaitAsync(timeout.Value).ConfigureAwait(false);

            _logger.LogInformation("Background indexing service stopped gracefully.");
            return true;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Background indexing task did not complete within {Timeout}s timeout. Proceeding with disposal anyway.",
                timeout.Value.TotalSeconds);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Indexing task cancellation acknowledged.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping indexing service.");
            return false;
        }
    }

    /// <summary>
    /// Synchronous wrapper for backwards compatibility with IDisposable.
    /// Signals cancellation and waits for the task to complete to ensure proper cleanup.
    /// </summary>
    private void StopIndexService()
    {
        // Dispose timer first - this causes WaitForNextTickAsync to return false
        _indexingTimer?.Dispose();
        _indexingTimer = null;

        if (_indexingTask is null || _indexingTask.IsCompleted)
            return;

        try
        {
            // Signal cancellation
            _shutdownCts.Cancel();

            // Wait for the task to complete gracefully
            _indexingTask.Wait(_options.ShutdownTimeout);
        }
        catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping indexing service synchronously.");
        }
    }

    /// <summary>
    /// Disposes the background indexing service synchronously.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Check if async disposal already happened (thread-safe single-entry)
                if (Interlocked.CompareExchange(ref _asyncDisposeStarted, 1, 0) == 0)
                {
                    _logger.LogInformation("Disposing BackgroundIndexService synchronously...");

                    // Stop indexing service (uses sync wrapper)
                    StopIndexService();

                    // Dispose timer if not already done
                    _indexingTimer?.Dispose();

                    // Dispose synchronization primitives
                    _shutdownCts.Dispose();

                    _logger.LogInformation("BackgroundIndexService disposed.");
                }
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Disposes the background indexing service synchronously.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the background indexing service asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        // Suppress finalization since we've disposed
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Core async disposal logic - handles async cleanup operations.
    /// </summary>
    private async ValueTask DisposeAsyncCore()
    {
        // Ensure single-entry with thread-safety
        if (Interlocked.CompareExchange(ref _asyncDisposeStarted, 1, 0) != 0)
        {
            return; // Already disposing
        }

        if (_disposedValue)
        {
            return;
        }

        _logger.LogInformation("Disposing BackgroundIndexService asynchronously...");

        try
        {
            // Step 1: Stop background indexing gracefully
            await StopAsync(_options.ShutdownTimeout).ConfigureAwait(false);

            // Step 2: Dispose the PeriodicTimer if not already done
            _indexingTimer?.Dispose();

            // Step 3: Dispose synchronization primitives
            _shutdownCts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async disposal.");
        }
        finally
        {
            _disposedValue = true;
        }

        _logger.LogInformation("BackgroundIndexService disposed.");
    }
}
