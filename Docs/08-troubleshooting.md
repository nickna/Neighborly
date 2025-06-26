# Troubleshooting Guide

## Overview

This guide helps diagnose and resolve common issues when working with Neighborly. It covers performance problems, configuration issues, deployment challenges, and debugging techniques.

## Common Issues and Solutions

### Installation and Setup Issues

#### .NET Runtime Not Found
**Symptoms:**
- "dotnet command not found" error
- Application fails to start with missing runtime error

**Solutions:**
```bash
# Install .NET 8 Runtime
# Ubuntu/Debian
sudo apt-get install -y dotnet-runtime-8.0

# CentOS/RHEL
sudo dnf install dotnet-runtime-8.0

# macOS
brew install dotnet

# Verify installation
dotnet --version
```

#### Aspire Workload Missing
**Symptoms:**
- Build errors related to Aspire workload
- Missing project templates

**Solution:**
```bash
dotnet workload install aspire
dotnet workload update
```

#### NuGet Package Installation Fails
**Symptoms:**
- Package not found errors
- Version conflicts

**Solutions:**
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# Use specific package source
dotnet add package Neighborly --source https://api.nuget.org/v3/index.json
```

### Database and File Issues

#### Database File Corruption
**Symptoms:**
- `InvalidDataException` when loading database
- Unexpected data or missing vectors
- Application crashes during database operations

**Diagnosis:**
```csharp
try
{
    await db.LoadAsync("vectors.db");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Database corruption detected: {ex.Message}");
    // Check file size and last modified date
    var fileInfo = new FileInfo("vectors.db");
    Console.WriteLine($"File size: {fileInfo.Length} bytes");
    Console.WriteLine($"Last modified: {fileInfo.LastWriteTime}");
}
```

**Solutions:**
1. **Restore from backup:**
   ```csharp
   // Load from backup file
   await db.LoadAsync("vectors_backup.db");
   ```

2. **Rebuild database:**
   ```csharp
   // Export data if partially readable
   try
   {
       await db.ExportDataAsync("recovery.json", ContentType.JSON);
       
       // Create new database and re-import
       var newDb = new VectorDatabase();
       await newDb.ImportDataAsync("recovery.json", false, ContentType.JSON);
       await newDb.SaveAsync("vectors_recovered.db");
   }
   catch (Exception ex)
   {
       Console.WriteLine($"Recovery failed: {ex.Message}");
   }
   ```

3. **Validate data integrity:**
   ```csharp
   // Check for consistency
   public async Task ValidateDatabase(VectorDatabase db)
   {
       var vectorCount = db.Count;
       var tagCount = db.Tags.GetAllTags().Count();
       
       Console.WriteLine($"Vectors: {vectorCount}, Tags: {tagCount}");
       
       // Verify each vector is accessible
       for (int i = 0; i < Math.Min(vectorCount, 100); i++)
       {
           try
           {
               var vector = db.Vectors[i];
               if (vector.Values == null || vector.Values.Length == 0)
               {
                   Console.WriteLine($"Invalid vector at index {i}");
               }
           }
           catch (Exception ex)
           {
               Console.WriteLine($"Error accessing vector {i}: {ex.Message}");
           }
       }
   }
   ```

#### File Permission Issues
**Symptoms:**
- `UnauthorizedAccessException` when saving/loading
- Cannot create database files

**Solutions:**
```bash
# Linux/macOS: Fix file permissions
sudo chown $USER:$USER /path/to/database/
chmod 755 /path/to/database/
chmod 644 /path/to/database/vectors.db

# Windows: Run as administrator or check folder permissions
# Use icacls to grant permissions:
icacls "C:\MyApp\Data" /grant Users:F
```

#### Disk Space Issues
**Symptoms:**
- `IOException` during save operations
- Database file partially written

**Diagnosis:**
```csharp
public void CheckDiskSpace(string path)
{
    var drive = new DriveInfo(Path.GetPathRoot(path));
    var freeSpace = drive.AvailableFreeSpace;
    var totalSpace = drive.TotalSize;
    
    Console.WriteLine($"Free space: {freeSpace / 1024 / 1024} MB");
    Console.WriteLine($"Total space: {totalSpace / 1024 / 1024} MB");
    
    if (freeSpace < 100 * 1024 * 1024) // Less than 100MB
    {
        Console.WriteLine("Warning: Low disk space!");
    }
}
```

**Solutions:**
- Free up disk space
- Move database to different drive
- Enable compression:
  ```csharp
  var config = new VectorDatabaseConfig { EnableCompression = true };
  ```

### Performance Issues

#### Slow Search Performance
**Symptoms:**
- Search queries taking too long
- High CPU usage during searches
- Application becoming unresponsive

**Diagnosis:**
```csharp
public async Task DiagnoseSearchPerformance()
{
    var db = new VectorDatabase();
    await db.LoadAsync("vectors.db");
    
    var query = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
    
    // Test different algorithms
    var algorithms = new[] 
    { 
        SearchAlgorithm.Linear, 
        SearchAlgorithm.KDTree, 
        SearchAlgorithm.BallTree, 
        SearchAlgorithm.HNSW 
    };
    
    foreach (var algorithm in algorithms)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var results = await db.SearchAsync(query, k: 10, algorithm: algorithm);
            sw.Stop();
            Console.WriteLine($"{algorithm}: {sw.ElapsedMilliseconds}ms ({results.Count} results)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{algorithm}: Failed - {ex.Message}");
        }
    }
    
    // Check index status
    Console.WriteLine($"Vector count: {db.Count}");
    Console.WriteLine($"Has outdated index: {db.HasOutdatedIndex}");
}
```

**Solutions:**

1. **Wait for index building:**
   ```csharp
   // Allow background indexing to complete
   await Task.Delay(TimeSpan.FromSeconds(10));
   
   // Or manually rebuild
   await db.RebuildSearchIndexesAsync();
   ```

2. **Choose appropriate algorithm:**
   ```csharp
   // For high-dimensional data
   var results = await db.SearchAsync(query, k: 10, SearchAlgorithm.BallTree);
   
   // For approximate search
   var results = await db.SearchAsync(query, k: 10, SearchAlgorithm.HNSW);
   ```

3. **Optimize for your data:**
   ```csharp
   // For low dimensions (< 20)
   SearchAlgorithm.KDTree
   
   // For high dimensions
   SearchAlgorithm.BallTree
   
   // For very large datasets
   SearchAlgorithm.HNSW
   ```

#### High Memory Usage
**Symptoms:**
- OutOfMemoryException
- Application using excessive RAM
- System becoming slow

**Diagnosis:**
```csharp
public void DiagnoseMemoryUsage()
{
    var beforeGC = GC.GetTotalMemory(false);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    var afterGC = GC.GetTotalMemory(true);
    
    Console.WriteLine($"Memory before GC: {beforeGC / 1024 / 1024} MB");
    Console.WriteLine($"Memory after GC: {afterGC / 1024 / 1024} MB");
    Console.WriteLine($"Memory freed: {(beforeGC - afterGC) / 1024 / 1024} MB");
    
    // Check vector list memory usage
    var process = Process.GetCurrentProcess();
    Console.WriteLine($"Working set: {process.WorkingSet64 / 1024 / 1024} MB");
    Console.WriteLine($"Private memory: {process.PrivateMemorySize64 / 1024 / 1024} MB");
}
```

**Solutions:**

1. **Use memory-mapped files:**
   ```csharp
   // Enable memory mapping for large datasets
   var db = new VectorDatabase();
   await db.LoadAsync("vectors.db", useMemoryMapping: true);
   ```

2. **Implement batching:**
   ```csharp
   // Process vectors in batches
   const int batchSize = 1000;
   for (int i = 0; i < vectors.Count; i += batchSize)
   {
       var batch = vectors.Skip(i).Take(batchSize);
       await db.AddRangeAsync(batch);
       
       // Optional: force garbage collection
       if (i % (batchSize * 10) == 0)
       {
           GC.Collect();
       }
   }
   ```

3. **Configure memory limits:**
   ```csharp
   // Set environment variables
   Environment.SetEnvironmentVariable("DOTNET_GCHeapHardLimit", "2GB");
   ```

#### Slow Database Loading
**Symptoms:**
- Long startup times
- Application hangs during LoadAsync

**Diagnosis:**
```csharp
public async Task DiagnoseLoadPerformance(string path)
{
    var fileInfo = new FileInfo(path);
    Console.WriteLine($"File size: {fileInfo.Length / 1024 / 1024} MB");
    
    var sw = Stopwatch.StartNew();
    
    var db = new VectorDatabase();
    await db.LoadAsync(path);
    
    sw.Stop();
    Console.WriteLine($"Load time: {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"Load rate: {fileInfo.Length / sw.ElapsedMilliseconds / 1024:F2} KB/ms");
    Console.WriteLine($"Vector count: {db.Count}");
}
```

**Solutions:**

1. **Use SSD storage:**
   - Move database files to SSD
   - Ensure adequate I/O bandwidth

2. **Optimize file format:**
   ```csharp
   // Use binary format for faster loading
   await db.SaveAsync("vectors.db", useBinaryFormat: true);
   ```

3. **Implement progressive loading:**
   ```csharp
   // Load database with progress reporting
   await db.LoadAsync(path, progress: new Progress<LoadProgress>(p => 
   {
       Console.WriteLine($"Loading: {p.Percentage:F1}% ({p.VectorsLoaded}/{p.TotalVectors})");
   }));
   ```

### Threading and Concurrency Issues

#### Deadlocks
**Symptoms:**
- Application hangs indefinitely
- Multiple threads waiting for each other

**Diagnosis:**
```csharp
// Enable deadlock detection
public class DeadlockDetector
{
    private static readonly Timer _timer = new Timer(DetectDeadlocks, null, 
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    
    private static void DetectDeadlocks(object state)
    {
        var threads = Process.GetCurrentProcess().Threads;
        foreach (ProcessThread thread in threads)
        {
            if (thread.ThreadState == ThreadState.Wait)
            {
                Console.WriteLine($"Thread {thread.Id} is waiting");
            }
        }
    }
}
```

**Solutions:**

1. **Use proper async patterns:**
   ```csharp
   // Don't block on async operations
   // BAD:
   db.SearchAsync(query).Wait();
   
   // GOOD:
   var results = await db.SearchAsync(query);
   ```

2. **Implement timeouts:**
   ```csharp
   using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
   try
   {
       var results = await db.SearchAsync(query, cancellationToken: cts.Token);
   }
   catch (OperationCanceledException)
   {
       Console.WriteLine("Operation timed out");
   }
   ```

3. **Avoid nested locks:**
   ```csharp
   // Structure code to avoid lock ordering issues
   // Always acquire locks in the same order
   ```

#### Race Conditions
**Symptoms:**
- Inconsistent search results
- Occasional exceptions
- Data corruption

**Solutions:**

1. **Use thread-safe operations:**
   ```csharp
   // Use proper locking for database modifications
   lock (_syncObject)
   {
       // Modify database state
   }
   ```

2. **Implement proper synchronization:**
   ```csharp
   // Use the database's built-in locking
   await db.AddAsync(vector); // Thread-safe
   var results = await db.SearchAsync(query); // Thread-safe
   ```

### Docker and Deployment Issues

#### Container Startup Failures
**Symptoms:**
- Container exits immediately
- Health checks failing
- Port binding errors

**Diagnosis:**
```bash
# Check container logs
docker logs neighborly-container

# Check port usage
netstat -tulpn | grep 8080

# Test container health
docker exec neighborly-container curl -f http://localhost:8080/health
```

**Solutions:**

1. **Fix port conflicts:**
   ```bash
   # Use different port
   docker run -p 8081:8080 nick206/neighborly:latest
   ```

2. **Check environment variables:**
   ```bash
   docker run -e PROTO_GRPC=true -e PROTO_REST=true nick206/neighborly:latest
   ```

3. **Verify volume mounts:**
   ```bash
   # Ensure data directory exists and has correct permissions
   mkdir -p ./data
   chmod 755 ./data
   docker run -v ./data:/app/data nick206/neighborly:latest
   ```

#### gRPC Connection Issues
**Symptoms:**
- "Connection refused" errors
- gRPC clients cannot connect
- Timeout exceptions

**Solutions:**

1. **Check gRPC configuration:**
   ```csharp
   // Client configuration
   var channel = GrpcChannel.ForAddress("http://localhost:8080");
   var client = new VectorService.VectorServiceClient(channel);
   ```

2. **Verify TLS settings:**
   ```csharp
   // For development (HTTP)
   var channel = GrpcChannel.ForAddress("http://localhost:8080");
   
   // For production (HTTPS)
   var channel = GrpcChannel.ForAddress("https://localhost:8443");
   ```

3. **Enable gRPC reflection:**
   ```json
   {
     "GRPC_REFLECTION": "true"
   }
   ```

### Platform-Specific Issues

#### Mobile Platform Limitations
**Symptoms:**
- Poor search performance on mobile
- Battery drain
- App backgrounding issues

**Solutions:**

1. **Disable background indexing:**
   ```csharp
   #if ANDROID || IOS
   var config = new VectorDatabaseConfig 
   { 
       BackgroundIndexing = false 
   };
   #endif
   ```

2. **Manual index rebuilding:**
   ```csharp
   // Rebuild indexes at strategic times
   public async Task OnAppResumed()
   {
       if (ShouldRebuildIndex())
       {
           await db.RebuildSearchIndexesAsync();
       }
   }
   ```

3. **Optimize for mobile:**
   ```csharp
   // Use smaller indexes and compression
   var config = new VectorDatabaseConfig
   {
       EnableCompression = true,
       MaxIndexSize = 1000000 // 1M vectors max
   };
   ```

## Debugging Techniques

### Logging Configuration

#### Enable Detailed Logging
```csharp
// Configure logging for troubleshooting
builder.Services.AddLogging(config =>
{
    config.SetMinimumLevel(LogLevel.Debug);
    config.AddConsole();
    config.AddDebug();
});

// Add structured logging
builder.Services.AddSerilog(config =>
{
    config.WriteTo.Console()
          .WriteTo.File("logs/neighborly-.txt", rollingInterval: RollingInterval.Day)
          .MinimumLevel.Debug();
});
```

#### Log Analysis
```bash
# Search for errors in logs
grep -i "error\|exception\|failed" logs/neighborly-*.txt

# Monitor real-time logs
tail -f logs/neighborly-$(date +%Y%m%d).txt

# Count error occurrences
grep -c "ERROR" logs/neighborly-*.txt
```

### Performance Profiling

#### Built-in Metrics
```csharp
// Enable metrics collection
builder.Services.AddSingleton<IMetrics, NeighborlyMetrics>();

// Monitor key metrics
public void MonitorPerformance(VectorDatabase db)
{
    Console.WriteLine($"Vector count: {db.Count}");
    Console.WriteLine($"Memory usage: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
    Console.WriteLine($"Search cache hit rate: {db.GetSearchCacheHitRate():P}");
}
```

#### External Tools
```bash
# Monitor with dotnet-counters
dotnet-counters monitor --process-id $(pgrep Neighborly) --counters System.Runtime,Microsoft.AspNetCore.Hosting

# Profile with dotnet-trace
dotnet-trace collect --process-id $(pgrep Neighborly) --duration 00:00:30

# Memory profiling with dotnet-dump
dotnet-dump collect --process-id $(pgrep Neighborly)
```

### Network Debugging

#### gRPC Debugging
```bash
# Enable gRPC logging
export GRPC_TRACE=all
export GRPC_VERBOSITY=DEBUG

# Test with grpcurl
grpcurl -plaintext localhost:8080 list
grpcurl -plaintext localhost:8080 VectorService/GetDatabaseInfo
```

#### REST API Testing
```bash
# Test REST endpoints
curl -X GET http://localhost:8080/health
curl -X POST http://localhost:8080/vectors \
  -H "Content-Type: application/json" \
  -d '{"values":[1.0,2.0,3.0]}'
```

## Emergency Procedures

### Data Recovery
```bash
#!/bin/bash
# Emergency data recovery script

# Create backup directory
mkdir -p /tmp/neighborly-recovery

# Copy corrupted database for analysis
cp vectors.db /tmp/neighborly-recovery/corrupted.db

# Try to export recoverable data
dotnet run --project RecoveryTool -- \
  --input corrupted.db \
  --output /tmp/neighborly-recovery/recovered.json \
  --format json

# Create new database from recovered data
dotnet run --project Neighborly -- \
  --import /tmp/neighborly-recovery/recovered.json \
  --output vectors-recovered.db
```

### Service Restart
```bash
#!/bin/bash
# Safe service restart procedure

# Graceful shutdown
sudo systemctl stop neighborly

# Verify process terminated
while pgrep Neighborly > /dev/null; do
  echo "Waiting for process to terminate..."
  sleep 1
done

# Check database integrity
dotnet /opt/neighborly/Neighborly.dll --verify-database

# Restart service
sudo systemctl start neighborly

# Verify service health
sleep 5
curl -f http://localhost:8080/health || {
  echo "Service health check failed"
  sudo systemctl status neighborly
  exit 1
}

echo "Service restarted successfully"
```

This troubleshooting guide provides comprehensive coverage of common issues and their solutions, enabling users to quickly diagnose and resolve problems with Neighborly deployments.