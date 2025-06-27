using NUnit.Framework;
using System.Collections.Concurrent;

namespace Neighborly.Tests;

[TestFixture]
public class MemoryMappedListConcurrencyTests
{
    private MemoryMappedList? _list;
    
    [SetUp]
    public void Setup()
    {
        _list = new MemoryMappedList(1000);
    }
    
    [TearDown]
    public void TearDown()
    {
        _list?.Dispose();
    }
    
    [Test]
    public void ConcurrentReads_ShouldNotCorruptData()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 100; i++)
        {
            var vector = new Vector(new float[] { i, i * 2, i * 3 });
            vectors.Add(vector);
            _list!.Add(vector);
        }
        
        var errors = new ConcurrentBag<Exception>();
        var readResults = new ConcurrentBag<Vector?>();
        
        // Act - 10 threads reading concurrently
        Parallel.For(0, 10, _ =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    var vector = _list!.GetVector(vectors[i].Id);
                    readResults.Add(vector);
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });
        
        // Assert
        Assert.That(errors, Is.Empty, "No exceptions should occur during concurrent reads");
        Assert.That(readResults.Count, Is.EqualTo(1000), "All reads should complete");
        Assert.That(readResults.Where(v => v != null).Count(), Is.EqualTo(1000), "All vectors should be found");
    }
    
    [Test]
    public void ConcurrentWrites_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var addedVectors = new ConcurrentBag<Vector>();
        var errors = new ConcurrentBag<Exception>();
        
        // Act - 5 threads adding vectors concurrently
        Parallel.For(0, 5, threadId =>
        {
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    var vector = new Vector(new float[] { threadId * 100 + i, i, threadId });
                    _list!.Add(vector);
                    addedVectors.Add(vector);
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });
        
        // Assert
        Assert.That(errors, Is.Empty, "No exceptions should occur during concurrent writes");
        Assert.That(_list!.Count, Is.EqualTo(100), "Count should match total added vectors");
        
        // Verify all vectors can be retrieved
        foreach (var vector in addedVectors)
        {
            var retrieved = _list.GetVector(vector.Id);
            Assert.That(retrieved, Is.Not.Null, $"Vector {vector.Id} should be retrievable");
        }
    }
    
    [Test]
    public async Task MixedReadsAndWrites_ShouldNotDeadlock()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var errors = new ConcurrentBag<Exception>();
        
        // Pre-populate with some data
        for (int i = 0; i < 50; i++)
        {
            _list!.Add(new Vector(new float[] { i, i * 2 }));
        }
        
        // Act - Mixed operations for 2 seconds
        var tasks = new List<Task>();
        
        // Writers
        for (int i = 0; i < 2; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    int counter = 0;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        _list!.Add(new Vector(new float[] { counter++, counter }));
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    errors.Add(ex);
                }
            }));
        }
        
        // Readers
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var count = _list!.Count;
                        if (count > 0)
                        {
                            _list.GetVector(Random.Shared.Next(0, (int)count));
                        }
                        Thread.Sleep(5);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    errors.Add(ex);
                }
            }));
        }
        
        // Enumerator
        tasks.Add(Task.Run(() =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    int enumCount = 0;
                    foreach (var v in _list!)
                    {
                        enumCount++;
                        if (enumCount > 10) break; // Don't enumerate all
                    }
                    Thread.Sleep(50);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                errors.Add(ex);
            }
        }));
        
        // Let it run for 2 seconds
        Thread.Sleep(2000);
        cts.Cancel();
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks.ToArray()).WaitAsync(TimeSpan.FromSeconds(5));
        
        // Assert
        Assert.That(errors, Is.Empty, "No exceptions should occur during mixed operations");
        Assert.That(_list!.Count, Is.GreaterThan(50), "Should have added more vectors");
    }
    
    [Test]
    public void Enumeration_ShouldNotBlockWrites()
    {
        // Arrange
        _list!.Add(new Vector(new float[] { 1, 2, 3 }));
        
        var enumerationStarted = new ManualResetEventSlim();
        var writeCompleted = new ManualResetEventSlim();
        Exception? enumerationError = null;
        Exception? writeError = null;
        
        // Act
        var enumerationTask = Task.Run(() =>
        {
            try
            {
                foreach (var vector in _list)
                {
                    enumerationStarted.Set();
                    // Simulate slow enumeration
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                enumerationError = ex;
            }
        });
        
        var writeTask = Task.Run(() =>
        {
            try
            {
                enumerationStarted.Wait();
                // Try to write while enumeration is happening
                _list.Add(new Vector(new float[] { 4, 5, 6 }));
                writeCompleted.Set();
            }
            catch (Exception ex)
            {
                writeError = ex;
            }
        });
        
        // Assert
        var writeCompletedInTime = writeCompleted.Wait(TimeSpan.FromSeconds(1));
        Assert.That(writeCompletedInTime, Is.True, "Write should complete quickly even during enumeration");
        Assert.That(writeError, Is.Null, "Write should not throw exception");
        
        Task.WaitAll(new[] { enumerationTask, writeTask }, TimeSpan.FromSeconds(5));
        Assert.That(enumerationError, Is.Null, "Enumeration should not throw exception");
    }
    
    [Test]
    public void Disposal_ShouldPreventFurtherOperations()
    {
        // Arrange
        var list = new MemoryMappedList(100);
        list.Add(new Vector(new float[] { 1, 2, 3 }));
        
        // Act
        list.Dispose();
        
        // Assert - All operations should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => list.Add(new Vector(new float[] { 4, 5, 6 })), "Add should throw");
        Assert.Throws<ObjectDisposedException>(() => list.GetVector(0), "GetVector(index) should throw");
        Assert.Throws<ObjectDisposedException>(() => list.GetVector(Guid.NewGuid()), "GetVector(guid) should throw");
        Assert.Throws<ObjectDisposedException>(() => { var _ = list.Count; }, "Count should throw");
        Assert.Throws<ObjectDisposedException>(() => list.Flush(), "Flush should throw");
    }
    
    [Test]
    public void PositionIndependentReads_ShouldWorkCorrectly()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 10; i++)
        {
            var v = new Vector(Enumerable.Range(i * 10, 10).Select(x => (float)x).ToArray());
            vectors.Add(v);
            _list!.Add(v);
        }
        
        // Act - Read vectors in parallel at different positions
        var readTasks = vectors.Select((v, index) => Task.Run(() =>
        {
            var retrieved = _list!.GetVector(index);
            return (Expected: v, Retrieved: retrieved);
        })).ToArray();
        
        var results = Task.WhenAll(readTasks).Result;
        
        // Assert
        foreach (var (expected, retrieved) in results)
        {
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.Id, Is.EqualTo(expected.Id));
            Assert.That(retrieved.Values, Is.EqualTo(expected.Values));
        }
    }
}