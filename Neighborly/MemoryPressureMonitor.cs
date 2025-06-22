using System.Runtime;

namespace Neighborly;

internal class MemoryPressureMonitor : IDisposable
{
    private readonly Timer _monitorTimer;
    private readonly List<WeakReference<MemoryMappedList>> _managedLists = new();
    private readonly object _lock = new();
    private long _lastGcMemory;
    private bool _disposedValue;

    public MemoryPressureMonitor(int checkIntervalMs = 30000) // Check every 30 seconds
    {
        _lastGcMemory = GC.GetTotalMemory(false);
        _monitorTimer = new Timer(CheckMemoryPressure, null, checkIntervalMs, checkIntervalMs);
    }

    public void RegisterList(MemoryMappedList list)
    {
        lock (_lock)
        {
            _managedLists.Add(new WeakReference<MemoryMappedList>(list));
        }
    }

    private void CheckMemoryPressure(object? state)
    {
        try
        {
            long currentMemory = GC.GetTotalMemory(false);
            long memoryDelta = currentMemory - _lastGcMemory;
            
            // Check if memory usage has increased significantly
            bool highPressure = memoryDelta > 100 * 1024 * 1024; // 100MB increase
            
            // Also check GC pressure
            var gcInfo = GC.GetGCMemoryInfo();
            bool gcPressure = gcInfo.MemoryLoadBytes > gcInfo.HighMemoryLoadThresholdBytes * 0.8;
            
            if (highPressure || gcPressure)
            {
                Logging.Logger.Information("Memory pressure detected. Current: {CurrentMB}MB, Delta: {DeltaMB}MB, GC Load: {LoadPercent}%", 
                    currentMemory / (1024 * 1024), 
                    memoryDelta / (1024 * 1024),
                    (gcInfo.MemoryLoadBytes * 100) / gcInfo.HighMemoryLoadThresholdBytes);
                
                RespondToMemoryPressure();
            }
            
            _lastGcMemory = currentMemory;
            
            // Cleanup dead references
            CleanupDeadReferences();
        }
        catch (Exception ex)
        {
            Logging.Logger.Warning(ex, "Error during memory pressure check");
        }
    }

    private void RespondToMemoryPressure()
    {
        lock (_lock)
        {
            foreach (var weakRef in _managedLists)
            {
                if (weakRef.TryGetTarget(out var list))
                {
                    try
                    {
                        // Force flush to ensure data is persisted
                        list.Flush();
                        
                        // Dispose stream handles to release memory-mapped memory
                        // This forces the OS to page out unused data
                        list.ReleaseMappedMemory();
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Warning(ex, "Failed to respond to memory pressure for a list");
                    }
                }
            }
        }
        
        // Force garbage collection
        GC.Collect(2, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        
        Logging.Logger.Information("Memory pressure response completed");
    }

    private void CleanupDeadReferences()
    {
        lock (_lock)
        {
            for (int i = _managedLists.Count - 1; i >= 0; i--)
            {
                if (!_managedLists[i].TryGetTarget(out _))
                {
                    _managedLists.RemoveAt(i);
                }
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _monitorTimer?.Dispose();
                lock (_lock)
                {
                    _managedLists.Clear();
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