using NUnit.Framework;
using Neighborly;

namespace Tests;

/// <summary>
/// Tests for FileGrowthStrategy class.
/// </summary>
[TestFixture]
public class FileGrowthStrategyTests
{
    [Test]
    public void ShouldGrow_BelowThreshold_ReturnsFalse()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        bool shouldGrow = strategy.ShouldGrow(1000, 800); // 80% usage
        Assert.That(shouldGrow, Is.False);
    }

    [Test]
    public void ShouldGrow_AtThreshold_ReturnsTrue()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        bool shouldGrow = strategy.ShouldGrow(1000, 900); // 90% usage
        Assert.That(shouldGrow, Is.True);
    }

    [Test]
    public void ShouldGrow_AboveThreshold_ReturnsTrue()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        bool shouldGrow = strategy.ShouldGrow(1000, 950); // 95% usage
        Assert.That(shouldGrow, Is.True);
    }

    [Test]
    public void ShouldGrow_ZeroCapacity_ReturnsTrue()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        bool shouldGrow = strategy.ShouldGrow(0, 0);
        Assert.That(shouldGrow, Is.True);
    }

    [Test]
    public void ShouldGrow_NegativeUsedSpace_ThrowsArgumentOutOfRange()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            strategy.ShouldGrow(1000, -100);
        });
    }

    [Test]
    public void CalculateNewCapacity_ExponentialRange_Returns1_5x()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        long current = 100 * 1024 * 1024; // 100 MB
        long newCapacity = strategy.CalculateNewCapacity(current, 0);

        long expected = 150 * 1024 * 1024; // 1.5x = 150 MB
        Assert.That(newCapacity, Is.EqualTo(expected));
    }

    [Test]
    public void CalculateNewCapacity_BeyondThreshold_ReturnsFixedIncrement()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        long current = 1024L * 1024 * 1024 + 1; // Just over 1 GB
        long newCapacity = strategy.CalculateNewCapacity(current, 0);

        long expected = current + 256 * 1024 * 1024; // +256 MB
        Assert.That(newCapacity, Is.EqualTo(expected));
    }

    [Test]
    public void CalculateNewCapacity_EnsuresRequiredSpace()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        long current = 100 * 1024 * 1024;
        long required = 200 * 1024 * 1024; // Require more than 1.5x

        long newCapacity = strategy.CalculateNewCapacity(current, required);
        Assert.That(newCapacity, Is.GreaterThanOrEqualTo(required));
    }

    [Test]
    public void CalculateNewCapacity_VeryLargeRequired_SkipsExponential()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        long current = 10 * 1024 * 1024; // 10 MB
        long required = 500 * 1024 * 1024; // 500 MB required

        long newCapacity = strategy.CalculateNewCapacity(current, required);
        Assert.That(newCapacity, Is.GreaterThanOrEqualTo(required));
    }

    [Test]
    public void CalculateNewCapacity_NegativeCurrentCapacity_ThrowsArgumentOutOfRange()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            strategy.CalculateNewCapacity(-100, 0);
        });
    }

    [Test]
    public void CalculateNewCapacity_NegativeRequiredSpace_ThrowsArgumentOutOfRange()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            strategy.CalculateNewCapacity(1000, -100);
        });
    }

    [Test]
    public void GetMinimumCapacityFor_ReturnsMaxOfInitialAndRequired()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        long initialCapacity = strategy.InitialCapacity;

        // Required less than initial
        long minCapacity = strategy.GetMinimumCapacityFor(initialCapacity / 2);
        Assert.That(minCapacity, Is.EqualTo(initialCapacity));

        // Required more than initial
        long required = initialCapacity * 2;
        minCapacity = strategy.GetMinimumCapacityFor(required);
        Assert.That(minCapacity, Is.EqualTo(required));
    }

    [Test]
    public void Constructor_InvalidInitialCapacity_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new FileGrowthStrategy(0);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new FileGrowthStrategy(-1000);
        });
    }

    [Test]
    public void Constructor_InvalidGrowthFactor_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new FileGrowthStrategy(1024, growthFactor: 1.0);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new FileGrowthStrategy(1024, growthFactor: 0.5);
        });
    }

    [Test]
    public void Constructor_InvalidThreshold_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new FileGrowthStrategy(1024, capacityThreshold: 0.0);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new FileGrowthStrategy(1024, capacityThreshold: 1.5);
        });
    }

    [Test]
    public void ForIndexFile_ReturnsStrategyWithSmallInitialCapacity()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        Assert.That(strategy.InitialCapacity, Is.EqualTo(1 * 1024 * 1024)); // 1 MB
        Assert.That(strategy.CapacityThreshold, Is.EqualTo(0.90));
    }

    [Test]
    public void ForDataFile_ReturnsStrategyWithLargerInitialCapacity()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        Assert.That(strategy.InitialCapacity, Is.EqualTo(10 * 1024 * 1024)); // 10 MB
        Assert.That(strategy.CapacityThreshold, Is.EqualTo(0.90));
    }
}

/// <summary>
/// Tests for RandomAccessFileHolder resize functionality.
/// </summary>
[TestFixture]
public class RandomAccessFileHolderResizeTests
{
    [Test]
    public void Constructor_WithGrowthStrategy_CreatesFileWithInitialCapacity()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        using var holder = new RandomAccessFileHolder(strategy);

        Assert.That(holder.Capacity, Is.EqualTo(strategy.InitialCapacity));
        Assert.That(File.Exists(holder.Filename), Is.True);
    }

    [Test]
    public void Constructor_WithFixedCapacity_CreatesFileWithSpecifiedCapacity()
    {
        long capacity = 5 * 1024 * 1024; // 5 MB
        using var holder = new RandomAccessFileHolder(capacity);

        Assert.That(holder.Capacity, Is.EqualTo(capacity));
        Assert.That(File.Exists(holder.Filename), Is.True);
    }

    [Test]
    public void ResizeFile_IncreasesCapacity()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        using var holder = new RandomAccessFileHolder(strategy);

        long originalCapacity = holder.Capacity;
        long newCapacity = originalCapacity * 2;

        holder.ResizeFile(newCapacity);

        Assert.That(holder.Capacity, Is.EqualTo(newCapacity));
    }

    [Test]
    public void ResizeFile_PreservesExistingData()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        using var holder = new RandomAccessFileHolder(strategy);

        // Write test data
        byte[] testData = new byte[5000];
        Random.Shared.NextBytes(testData);
        holder.Write(0, testData);
        holder.FlushToDisk();

        long originalCapacity = holder.Capacity;

        // Resize
        holder.ResizeFile(originalCapacity * 2);

        // Verify data preserved
        byte[] readData = new byte[5000];
        holder.ReadExactly(0, readData);

        Assert.That(readData, Is.EqualTo(testData));
    }

    [Test]
    public void ResizeFile_ThrowsForSmallerCapacity()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        using var holder = new RandomAccessFileHolder(strategy);

        long originalCapacity = holder.Capacity;

        Assert.Throws<ArgumentException>(() =>
        {
            holder.ResizeFile(originalCapacity / 2);
        });
    }

    [Test]
    public void ResizeFile_ThrowsForEqualCapacity()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        using var holder = new RandomAccessFileHolder(strategy);

        long originalCapacity = holder.Capacity;

        Assert.Throws<ArgumentException>(() =>
        {
            holder.ResizeFile(originalCapacity);
        });
    }

    [Test]
    public async Task ResizeFileAsync_WorksCorrectly()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        using var holder = new RandomAccessFileHolder(strategy);

        // Write test data
        byte[] testData = new byte[1000];
        Random.Shared.NextBytes(testData);
        await holder.WriteAsync(0, testData);
        await holder.FlushToDiskAsync();

        long originalCapacity = holder.Capacity;
        long newCapacity = originalCapacity * 2;

        // Resize asynchronously
        await holder.ResizeFileAsync(newCapacity);

        Assert.That(holder.Capacity, Is.EqualTo(newCapacity));

        // Verify data preserved
        byte[] readData = new byte[1000];
        await holder.ReadExactlyAsync(0, readData);

        Assert.That(readData, Is.EqualTo(testData));
    }

    [Test]
    public void CheckAndCalculateGrowth_NoStrategy_ReturnsNull()
    {
        // Legacy constructor without growth
        using var holder = new RandomAccessFileHolder(1024 * 1024);

        long? newCapacity = holder.CheckAndCalculateGrowth(500 * 1024);

        Assert.That(newCapacity, Is.Null);
    }

    [Test]
    public void CheckAndCalculateGrowth_BelowThreshold_ReturnsNull()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        using var holder = new RandomAccessFileHolder(strategy);

        // Write some data but stay below 90%
        byte[] data = new byte[100];
        holder.Write(0, data);

        long? newCapacity = holder.CheckAndCalculateGrowth(100);

        Assert.That(newCapacity, Is.Null);
    }

    [Test]
    public void CheckAndCalculateGrowth_AtThreshold_ReturnsNewCapacity()
    {
        var strategy = new FileGrowthStrategy(1000, 1.5, 1024L * 1024 * 1024, 256L * 1024 * 1024, 0.90);
        using var holder = new RandomAccessFileHolder(strategy);

        // Write to 90% capacity
        byte[] data = new byte[900];
        holder.Write(0, data);

        long? newCapacity = holder.CheckAndCalculateGrowth(0);

        Assert.That(newCapacity, Is.Not.Null);
        Assert.That(newCapacity.Value, Is.GreaterThan(holder.Capacity));
    }

    [Test]
    public void CheckAndCalculateGrowth_RequiredExceedsCapacity_ReturnsNewCapacity()
    {
        var strategy = FileGrowthStrategy.ForIndexFile();
        using var holder = new RandomAccessFileHolder(strategy);

        long currentCapacity = holder.Capacity;
        long requiredSpace = currentCapacity + 1000; // Exceeds current capacity

        long? newCapacity = holder.CheckAndCalculateGrowth(requiredSpace);

        Assert.That(newCapacity, Is.Not.Null);
        Assert.That(newCapacity.Value, Is.GreaterThanOrEqualTo(requiredSpace));
    }

    [Test]
    public void ResizeFile_MultipleTimes_WorksCorrectly()
    {
        var strategy = FileGrowthStrategy.ForDataFile();
        using var holder = new RandomAccessFileHolder(strategy);

        // Write some initial data
        byte[] testData = new byte[1000];
        Random.Shared.NextBytes(testData);
        holder.Write(0, testData);

        long capacity1 = holder.Capacity;
        holder.ResizeFile(capacity1 * 2);
        long capacity2 = holder.Capacity;

        Assert.That(capacity2, Is.EqualTo(capacity1 * 2));

        // Resize again
        holder.ResizeFile(capacity2 * 2);
        long capacity3 = holder.Capacity;

        Assert.That(capacity3, Is.EqualTo(capacity2 * 2));

        // Verify original data still intact
        byte[] readData = new byte[1000];
        holder.ReadExactly(0, readData);
        Assert.That(readData, Is.EqualTo(testData));
    }
}

/// <summary>
/// Integration tests for MemoryMappedList with automatic file growth.
/// </summary>
[TestFixture]
public class MemoryMappedListGrowthIntegrationTests
{
    [Test]
    public void Constructor_WithGrowth_UsesSmallInitialCapacity()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: true);

        // Should start with small capacity (not pre-allocated for 1000s of vectors)
        var fileInfo = list.GetFileInfo();
        long indexCapacity = fileInfo[1];
        long dataCapacity = fileInfo[3];

        // Initial capacities should be small
        Assert.That(indexCapacity, Is.LessThan(10 * 1024 * 1024)); // < 10 MB
        Assert.That(dataCapacity, Is.LessThan(50 * 1024 * 1024)); // < 50 MB
    }

    [Test]
    public void Add_GrowsAutomatically_WhenCapacityExceeded()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: true);

        // Add many vectors to trigger growth
        int vectorCount = 100;
        var addedIds = new List<Guid>();

        for (int i = 0; i < vectorCount; i++)
        {
            var vector = new Vector(new float[128]); // 512 bytes each
            list.Add(vector);
            addedIds.Add(vector.Id);
        }

        Assert.That(list.Count, Is.EqualTo(vectorCount));

        // Verify all vectors retrievable
        foreach (var id in addedIds)
        {
            var vector = list.GetVector(id);
            Assert.That(vector, Is.Not.Null);
        }
    }

    [Test]
    public async Task AddAsync_GrowsAutomatically()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: true);

        // Add vectors
        var addedIds = new List<Guid>();
        int vectorCount = 50;

        for (int i = 0; i < vectorCount; i++)
        {
            var vector = new Vector(new float[256]);
            await list.AddAsync(vector);
            addedIds.Add(vector.Id);
        }

        Assert.That(list.Count, Is.EqualTo(vectorCount));

        // Verify all retrievable
        foreach (var id in addedIds)
        {
            var vector = await list.GetVectorAsync(id);
            Assert.That(vector, Is.Not.Null);
        }
    }

    [Test]
    public void Update_GrowsWhenExpandingVector()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: true);

        // Add small vector
        var vector = new Vector(new float[32]);
        list.Add(vector);

        // Update to much larger (triggers growth)
        var largeVector = new Vector(new float[2048]) { Id = vector.Id };
        bool updated = list.Update(largeVector);

        Assert.That(updated, Is.True);

        var retrieved = list.GetVector(vector.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Values.Length, Is.EqualTo(2048));
    }

    [Test]
    public void LegacyConstructor_WorksWithoutGrowth()
    {
        // Old constructor - should work as before
        using var list = new MemoryMappedList(capacity: 1000, FlushPolicy.Batched);

        // Add vectors
        for (int i = 0; i < 50; i++)
        {
            list.Add(new Vector(new float[128]));
        }

        Assert.That(list.Count, Is.EqualTo(50));
    }

    [Test]
    public void DisableGrowth_WorksWithReasonableDataset()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: false);

        // Should work for reasonable dataset
        for (int i = 0; i < 100; i++)
        {
            list.Add(new Vector(new float[128]));
        }

        Assert.That(list.Count, Is.EqualTo(100));
    }

    [Test]
    public void LargeVectorDataset_GrowsMultipleTimes()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: true);

        // Add enough data to trigger multiple growth cycles
        int vectorCount = 200;
        var addedIds = new List<Guid>();

        for (int i = 0; i < vectorCount; i++)
        {
            var vector = new Vector(new float[512]);
            list.Add(vector);
            addedIds.Add(vector.Id);
        }

        Assert.That(list.Count, Is.EqualTo(vectorCount));

        // Verify all vectors retrievable after multiple growth cycles
        foreach (var id in addedIds)
        {
            var vector = list.GetVector(id);
            Assert.That(vector, Is.Not.Null);
            Assert.That(vector!.Values.Length, Is.EqualTo(512));
        }

        // Verify fragmentation remains reasonable
        long fragmentation = list.CalculateFragmentation();
        Assert.That(fragmentation, Is.LessThan(100), "Fragmentation should remain reasonable after growth");
    }

    [Test]
    public void MixedOperations_WithGrowth_MaintainsDataIntegrity()
    {
        using var list = new MemoryMappedList(FlushPolicy.Batched, enableGrowth: true);

        var trackedVectors = new Dictionary<Guid, int>(); // Id -> dimension

        // Mix of adds
        for (int i = 0; i < 50; i++)
        {
            int dimensions = 100 + (i * 10);
            var vector = new Vector(new float[dimensions]);
            list.Add(vector);
            trackedVectors[vector.Id] = dimensions;
        }

        // Mix of updates
        int updateCount = 0;
        foreach (var kvp in trackedVectors.Take(10))
        {
            var updatedVector = new Vector(new float[kvp.Value * 2]) { Id = kvp.Key };
            bool updated = list.Update(updatedVector);
            Assert.That(updated, Is.True);
            trackedVectors[kvp.Key] = kvp.Value * 2;
            updateCount++;
        }

        // Mix of removes
        int removeCount = 0;
        foreach (var kvp in trackedVectors.Take(5))
        {
            var vectorToRemove = list.GetVector(kvp.Key);
            Assert.That(vectorToRemove, Is.Not.Null);
            bool removed = list.Remove(vectorToRemove!);
            Assert.That(removed, Is.True);
            removeCount++;
        }

        // Verify final count
        Assert.That(list.Count, Is.EqualTo(50 - removeCount));

        // Verify all non-removed vectors are still retrievable with correct dimensions
        foreach (var kvp in trackedVectors.Skip(removeCount))
        {
            var vector = list.GetVector(kvp.Key);
            Assert.That(vector, Is.Not.Null);
            Assert.That(vector!.Values.Length, Is.EqualTo(kvp.Value));
        }
    }
}
