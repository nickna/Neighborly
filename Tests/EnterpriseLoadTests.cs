using Microsoft.Extensions.Logging;
using Neighborly;
using Neighborly.Search;
using Neighborly.Tests.Helpers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Neighborly.Tests;

/// <summary>
/// Enterprise-scale load tests for verifying VectorDatabase performance
/// under high-volume concurrent workloads.
/// </summary>
[TestFixture]
[Category("Enterprise")]
public class EnterpriseLoadTests
{
    private VectorDatabase _db = null!;
    private MockLogger<VectorDatabase> _logger = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new MockLogger<VectorDatabase>();
        _db = new VectorDatabase(_logger, null);
    }

    [TearDown]
    public void TearDown()
    {
        _db?.Dispose();
    }

    /// <summary>
    /// Verifies the database performs well under realistic read-heavy workloads (1000:1 read:write ratio).
    /// </summary>
    [Test]
    public async Task HighReadWriteRatio_MaintainsPerformance()
    {
        // Arrange - Pre-populate with vectors
        const int initialVectors = 1000;
        var vectors = CreateRandomVectors(initialVectors, TestConstants.Dimensions.Standard);
        foreach (var v in vectors)
        {
            _db.AddVector(v);
        }
        await _db.RebuildSearchIndexesAsync();

        var readCount = TestConstants.Enterprise.ReadWriteRatio;
        var searchTimes = new ConcurrentBag<long>();
        var errors = new ConcurrentBag<Exception>();
        var queryVector = CreateRandomVector(TestConstants.Dimensions.Standard);

        // Act - Execute mixed read/write workload
        var readTasks = Enumerable.Range(0, readCount).Select(_ => Task.Run(() =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var results = _db.Search(queryVector, TestConstants.Search.DefaultK, SearchAlgorithm.Linear);
                sw.Stop();
                searchTimes.Add(sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }));

        var writeTask = Task.Run(() =>
        {
            try
            {
                _db.AddVector(CreateRandomVector(TestConstants.Dimensions.Standard));
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        await Task.WhenAll(readTasks.Append(writeTask));

        // Assert
        Assert.That(errors, Is.Empty, "No errors should occur during mixed workload");
        Assert.That(searchTimes.Count, Is.EqualTo(readCount), "All searches should complete");

        var avgSearchTime = searchTimes.Average();
        var maxSearchTime = searchTimes.Max();
        Console.WriteLine($"Avg search time: {avgSearchTime:F2}ms, Max: {maxSearchTime}ms");

        Assert.That(maxSearchTime, Is.LessThan(TestConstants.Benchmarks.MaxSearchTimeLargeMs),
            $"Maximum search time {maxSearchTime}ms should be under {TestConstants.Benchmarks.MaxSearchTimeLargeMs}ms");
    }

    /// <summary>
    /// Verifies concurrent access to 10K+ vector database scales without severe degradation.
    /// </summary>
    [Test]
    [Explicit("Long-running enterprise scale test")]
    public async Task LargeDatasetConcurrentAccess_ScalesReasonably()
    {
        // Arrange - Large dataset
        const int vectorCount = TestConstants.Enterprise.LargeDatasetSize;
        var vectors = CreateRandomVectors(vectorCount, TestConstants.Dimensions.Standard);
        foreach (var v in vectors)
        {
            _db.AddVector(v);
        }
        await _db.RebuildSearchIndexesAsync();

        var completedReads = 0;
        var completedWrites = 0;
        var errors = new ConcurrentBag<Exception>();
        var cts = new CancellationTokenSource();

        // Act - Run concurrent operations for fixed duration
        var readTasks = Enumerable.Range(0, TestConstants.Enterprise.HighReadLoadThreads)
            .Select(_ => Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var query = CreateRandomVector(TestConstants.Dimensions.Standard);
                        _db.Search(query, TestConstants.Search.DefaultK, SearchAlgorithm.Linear);
                        Interlocked.Increment(ref completedReads);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(ex);
                    }
                }
            }));

        var writeTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    _db.AddVector(CreateRandomVector(TestConstants.Dimensions.Standard));
                    Interlocked.Increment(ref completedWrites);
                    Thread.Sleep(TestConstants.Timeouts.OperationDelayMs);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        });

        // Run for configured duration
        await Task.Delay(TestConstants.Timeouts.MixedOperationsDurationMs);
        cts.Cancel();

        await Task.WhenAll(readTasks.Append(writeTask));

        // Assert
        Assert.That(errors, Is.Empty, "No exceptions during concurrent access");
        Assert.That(completedReads, Is.GreaterThan(0), "Should complete reads");
        Assert.That(completedWrites, Is.GreaterThan(0), "Should complete writes");

        Console.WriteLine($"Completed {completedReads} reads, {completedWrites} writes");
        Console.WriteLine($"Database size: {_db.Count} vectors");
    }

    /// <summary>
    /// Verifies performance remains stable over extended operation periods.
    /// </summary>
    [Test]
    [Explicit("Long-running sustained load test")]
    public async Task SustainedLoad_NoPerformanceDegradation()
    {
        // Arrange - Initial dataset
        const int initialSize = 1000;
        var vectors = CreateRandomVectors(initialSize, TestConstants.Dimensions.Standard);
        foreach (var v in vectors)
        {
            _db.AddVector(v);
        }
        await _db.RebuildSearchIndexesAsync();

        var queryVector = CreateRandomVector(TestConstants.Dimensions.Standard);

        // Baseline measurement
        var baselineTimes = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            _db.Search(queryVector, TestConstants.Search.DefaultK, SearchAlgorithm.Linear);
            sw.Stop();
            baselineTimes.Add(sw.ElapsedMilliseconds);
        }
        var baselineAvg = baselineTimes.Average();

        // Act - Sustained load
        var stopwatch = Stopwatch.StartNew();
        var operationCount = 0;
        while (stopwatch.ElapsedMilliseconds < TestConstants.Enterprise.SustainedLoadDurationMs)
        {
            // Mix of operations
            _db.AddVector(CreateRandomVector(TestConstants.Dimensions.Standard));
            for (int i = 0; i < 10; i++)
            {
                _db.Search(queryVector, TestConstants.Search.DefaultK, SearchAlgorithm.Linear);
            }
            operationCount++;
        }

        // Post-load measurement
        var postLoadTimes = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            _db.Search(queryVector, TestConstants.Search.DefaultK, SearchAlgorithm.Linear);
            sw.Stop();
            postLoadTimes.Add(sw.ElapsedMilliseconds);
        }
        var postLoadAvg = postLoadTimes.Average();

        // Assert
        var degradationPercent = baselineAvg > 0 ? ((postLoadAvg - baselineAvg) / baselineAvg) * 100 : 0;
        Console.WriteLine($"Baseline: {baselineAvg:F2}ms, Post-load: {postLoadAvg:F2}ms");
        Console.WriteLine($"Performance change: {degradationPercent:F1}%");
        Console.WriteLine($"Total operations: {operationCount}, Final DB size: {_db.Count}");

        Assert.That(degradationPercent, Is.LessThan(TestConstants.Enterprise.MaxPerformanceDegradationPercent),
            $"Performance should not degrade more than {TestConstants.Enterprise.MaxPerformanceDegradationPercent}%");
    }

    #region Helper Methods

    private static Vector CreateRandomVector(int dimensions)
    {
        var random = Random.Shared;
        var values = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            values[i] = (float)(random.NextDouble() * 2 - 1);
        }
        return new Vector(values, $"vector_{Guid.NewGuid():N}");
    }

    private static List<Vector> CreateRandomVectors(int count, int dimensions)
    {
        var vectors = new List<Vector>(count);
        for (int i = 0; i < count; i++)
        {
            vectors.Add(CreateRandomVector(dimensions));
        }
        return vectors;
    }

    #endregion
}
