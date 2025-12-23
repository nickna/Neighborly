using Neighborly.Tests.Helpers;
using NUnit.Framework;

namespace Neighborly.Tests;

[TestFixture]
public class MemoryMappedListAsyncTests
{
    private MemoryMappedList? _list;

    [SetUp]
    public void Setup()
    {
        _list = new MemoryMappedList(TestConstants.MemoryMapped.DefaultCapacity);
    }

    [TearDown]
    public void TearDown()
    {
        _list?.Dispose();
    }

    [Test]
    public async Task AddAsync_ShouldAddVector()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });

        // Act
        await _list!.AddAsync(vector);

        // Assert
        Assert.That(_list.Count, Is.EqualTo(1));
        var retrieved = _list.GetVector(vector.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(vector.Id));
    }

    [Test]
    public async Task AddAsync_MultipleVectors_ShouldAddAll()
    {
        // Arrange & Act
        var vectors = new List<Vector>();
        for (int i = 0; i < TestConstants.Counts.Default; i++)
        {
            var vector = new Vector(new float[] { i, i * 2, i * 3 });
            vectors.Add(vector);
            await _list!.AddAsync(vector);
        }

        // Assert
        Assert.That(_list!.Count, Is.EqualTo(TestConstants.Counts.Default));
        foreach (var vector in vectors)
        {
            var retrieved = _list.GetVector(vector.Id);
            Assert.That(retrieved, Is.Not.Null);
        }
    }

    [Test]
    public async Task GetVectorAsync_ById_ShouldReturnVector()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });
        await _list!.AddAsync(vector);

        // Act
        var retrieved = await _list.GetVectorAsync(vector.Id);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(vector.Id));
        Assert.That(retrieved.Values, Is.EqualTo(vector.Values));
    }

    [Test]
    public async Task GetVectorAsync_ByIndex_ShouldReturnVector()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });
        await _list!.AddAsync(vector);

        // Act
        var retrieved = await _list.GetVectorAsync(0);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(vector.Id));
    }

    [Test]
    public async Task GetVectorAsync_NonExistent_ShouldReturnNull()
    {
        // Act
        var retrieved = await _list!.GetVectorAsync(Guid.NewGuid());

        // Assert
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task RemoveAsync_ShouldRemoveVector()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });
        await _list!.AddAsync(vector);
        Assert.That(_list.Count, Is.EqualTo(1));

        // Act
        var result = await _list.RemoveAsync(vector);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(_list.Count, Is.EqualTo(0));
        var retrieved = await _list.GetVectorAsync(vector.Id);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task RemoveAsync_NonExistent_ShouldReturnFalse()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });

        // Act
        var result = await _list!.RemoveAsync(vector);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateVector()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });
        var vectorId = vector.Id;
        await _list!.AddAsync(vector);

        // Act - create updated vector and set the same ID (uses InternalsVisibleTo)
        var updatedVector = new Vector(new float[] { 4, 5, 6, 7 });
        updatedVector.Id = vectorId;
        var result = await _list.UpdateAsync(updatedVector);

        // Assert
        Assert.That(result, Is.True);
        var retrieved = await _list.GetVectorAsync(vectorId);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Values, Is.EqualTo(new float[] { 4, 5, 6, 7 }));
    }

    [Test]
    public async Task UpdateAsync_NonExistent_ShouldReturnFalse()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });

        // Act
        var result = await _list!.UpdateAsync(vector);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task FlushAsync_ShouldComplete()
    {
        // Arrange
        var vector = new Vector(new float[] { 1, 2, 3 });
        await _list!.AddAsync(vector);

        // Act & Assert - should not throw
        await _list.FlushAsync();
    }

    [Test]
    public async Task IAsyncEnumerable_ShouldEnumerateAllVectors()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < TestConstants.Counts.Default; i++)
        {
            var vector = new Vector(new float[] { i, i * 2, i * 3 });
            vectors.Add(vector);
            await _list!.AddAsync(vector);
        }

        // Act
        var enumerated = new List<Vector>();
        await foreach (var vector in _list!)
        {
            enumerated.Add(vector);
        }

        // Assert
        Assert.That(enumerated.Count, Is.EqualTo(TestConstants.Counts.Default));
        foreach (var original in vectors)
        {
            Assert.That(enumerated.Any(v => v.Id == original.Id), Is.True);
        }
    }

    [Test]
    public async Task IAsyncEnumerable_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        for (int i = 0; i < TestConstants.Counts.Default; i++)
        {
            await _list!.AddAsync(new Vector(new float[] { i, i * 2, i * 3 }));
        }

        var cts = new CancellationTokenSource();
        var enumerated = 0;

        // Act & Assert
        try
        {
            await foreach (var vector in _list!.WithCancellation(cts.Token))
            {
                enumerated++;
                if (enumerated >= 2)
                {
                    cts.Cancel();
                }
            }
            Assert.Fail("Expected OperationCanceledException");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Assert.That(enumerated, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task ConcurrentAsyncOperations_ShouldMaintainDataIntegrity()
    {
        // Arrange
        var tasks = new List<Task>();
        var addedVectors = new System.Collections.Concurrent.ConcurrentBag<Vector>();

        // Act - Multiple concurrent async adds
        for (int i = 0; i < TestConstants.Parallelism.DefaultThreads; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var vector = new Vector(new float[] { threadId * 100 + j, j });
                    await _list!.AddAsync(vector);
                    addedVectors.Add(vector);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.That(_list!.Count, Is.EqualTo(TestConstants.Parallelism.DefaultThreads * 10));
        foreach (var vector in addedVectors)
        {
            var retrieved = await _list.GetVectorAsync(vector.Id);
            Assert.That(retrieved, Is.Not.Null, $"Vector {vector.Id} should be retrievable");
        }
    }

    [Test]
    public async Task MixedSyncAndAsyncOperations_ShouldWork()
    {
        // Arrange
        var syncVector = new Vector(new float[] { 1, 2, 3 });
        var asyncVector = new Vector(new float[] { 4, 5, 6 });

        // Act
        _list!.Add(syncVector);
        await _list.AddAsync(asyncVector);

        // Assert
        Assert.That(_list.Count, Is.EqualTo(2));

        // Sync get of async-added vector
        var syncRetrieved = _list.GetVector(asyncVector.Id);
        Assert.That(syncRetrieved, Is.Not.Null);

        // Async get of sync-added vector
        var asyncRetrieved = await _list.GetVectorAsync(syncVector.Id);
        Assert.That(asyncRetrieved, Is.Not.Null);
    }

    [Test]
    public async Task CancellationToken_ShouldCancelOperation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - operations should throw OperationCanceledException
        try
        {
            await _list!.AddAsync(new Vector(new float[] { 1, 2, 3 }), cts.Token);
            Assert.Fail("Expected OperationCanceledException");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }
}
