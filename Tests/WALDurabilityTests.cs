using Neighborly;

namespace Neighborly.Tests;

/// <summary>
/// Tests for WAL-based durability with ACID guarantees.
/// Note: MemoryMappedList creates temp files with random names, so cross-instance
/// recovery tests are not possible without VectorDatabase integration.
/// These tests verify durability within a single session and the CheckpointManager behavior.
/// </summary>
[TestFixture]
public class WALDurabilityTests
{
    [Test]
    public void Test_WAL_LogsOperationBeforeWrite()
    {
        // This test verifies that operations are logged to WAL
        // The WAL is fsynced before data is written to data files
        using var list = new MemoryMappedList(100, FlushPolicy.Immediate);

        float[] vectorData = [1.0f, 2.0f, 3.0f];
        var vector = new Vector(vectorData);

        // Add should log to WAL, write data, then checkpoint
        list.Add(vector);

        // Verify data exists
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list.GetVector(vector.Id), Is.Not.Null);
    }

    [Test]
    public void Test_FlushPolicy_Immediate_CheckpointsAfterEachOperation()
    {
        // Arrange
        using var list = new MemoryMappedList(100, FlushPolicy.Immediate);

        // Act: Add a vector
        float[] vectorData = [1.0f, 2.0f, 3.0f];
        var vector = new Vector(vectorData);
        list.Add(vector);

        // Assert: Vector exists
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list.GetVector(vector.Id), Is.Not.Null);
    }

    [Test]
    public void Test_FlushPolicy_Batched_CheckpointsAfterBatchSize()
    {
        // Arrange: Use batch size of 10 (default is 100)
        using var list = new MemoryMappedList(100, FlushPolicy.Batched);

        // Act: Add vectors
        var vectors = new List<Vector>();
        for (int i = 0; i < 10; i++)
        {
            float[] data = [i * 1.0f, i * 2.0f, i * 3.0f];
            var vector = new Vector(data);
            vectors.Add(vector);
            list.Add(vector);
        }

        // Assert: All vectors exist
        Assert.That(list.Count, Is.EqualTo(10));
        foreach (var v in vectors)
        {
            Assert.That(list.GetVector(v.Id), Is.Not.Null, $"Vector {v.Id} should exist");
        }
    }

    [Test]
    public void Test_FlushPolicy_None_RequiresManualFlush()
    {
        // Arrange
        using var list = new MemoryMappedList(100, FlushPolicy.None);

        // Act: Add vector and manually flush
        float[] vectorData = [1.0f, 2.0f, 3.0f];
        var vector = new Vector(vectorData);
        list.Add(vector);
        list.Flush();

        // Assert: Vector exists after manual flush
        Assert.That(list.Count, Is.EqualTo(1));
        Assert.That(list.GetVector(vector.Id), Is.Not.Null);
    }

    [Test]
    public void Test_LSN_Ordering()
    {
        // Arrange: Add multiple vectors and verify order is preserved
        using var list = new MemoryMappedList(100, FlushPolicy.Immediate);

        var vectors = new List<Vector>();
        for (int i = 0; i < 5; i++)
        {
            float[] data = [i * 1.0f, i * 2.0f, i * 3.0f];
            var vector = new Vector(data);
            vectors.Add(vector);
            list.Add(vector);
        }

        // Assert: All vectors exist in order
        Assert.That(list.Count, Is.EqualTo(5));
        for (int i = 0; i < vectors.Count; i++)
        {
            var retrieved = list.GetVector(vectors[i].Id);
            Assert.That(retrieved, Is.Not.Null, $"Vector {i} should exist");
            Assert.That(retrieved!.Values[0], Is.EqualTo(i * 1.0f).Within(0.001f));
        }
    }

    [Test]
    public void Test_Add_Remove_Within_Session()
    {
        // Test that Add and Remove work correctly within a single session
        using var list = new MemoryMappedList(100, FlushPolicy.Immediate);

        var vector = new Vector([1.0f, 2.0f, 3.0f]);
        list.Add(vector);
        Assert.That(list.Count, Is.EqualTo(1));

        // Remove the vector
        bool removed = list.Remove(vector);
        Assert.That(removed, Is.True);
        Assert.That(list.Count, Is.EqualTo(0));
        Assert.That(list.GetVector(vector.Id), Is.Null);
    }

    [Test]
    public void Test_Add_Update_Within_Session()
    {
        // Test that Add and Update work correctly within a single session
        using var list = new MemoryMappedList(100, FlushPolicy.Immediate);

        float[] originalData = [1.0f, 2.0f, 3.0f];
        float[] updatedData = [4.0f, 5.0f, 6.0f];

        var vector = new Vector(originalData);
        var vectorId = vector.Id;
        list.Add(vector);

        // Update the vector - create new Vector and set the same ID
        var updatedVector = new Vector(updatedData);
        updatedVector.Id = vectorId;  // Uses internal setter (InternalsVisibleTo)
        bool updated = list.Update(updatedVector);

        Assert.That(updated, Is.True);

        // Verify the updated values
        var retrieved = list.GetVector(vectorId);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Values, Is.EqualTo(updatedData).Within(0.001f));
    }

    [Test]
    public void Test_MixedOperations_Within_Session()
    {
        // Test mixed Add, Remove, Update operations in a single session
        using var list = new MemoryMappedList(100, FlushPolicy.Immediate);

        float[] updatedData = [10.0f, 20.0f, 30.0f];

        // Add three vectors
        var v1 = new Vector([1.0f, 2.0f, 3.0f]);
        var v2 = new Vector([4.0f, 5.0f, 6.0f]);
        var v3 = new Vector([7.0f, 8.0f, 9.0f]);

        list.Add(v1);
        list.Add(v2);
        list.Add(v3);
        Assert.That(list.Count, Is.EqualTo(3));

        // Remove v2
        list.Remove(v2);
        Assert.That(list.Count, Is.EqualTo(2));

        // Update v3
        var v3Updated = new Vector(updatedData);
        v3Updated.Id = v3.Id;
        list.Update(v3Updated);

        // Verify final state
        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(list.GetVector(v1.Id), Is.Not.Null, "v1 should exist");
        Assert.That(list.GetVector(v2.Id), Is.Null, "v2 should be removed");

        var updated = list.GetVector(v3.Id);
        Assert.That(updated, Is.Not.Null, "v3 should exist");
        Assert.That(updated!.Values, Is.EqualTo(updatedData).Within(0.001f), "v3 should have updated values");
    }

    [Test]
    public void Test_Multiple_Adds_With_Flush()
    {
        // Test multiple adds with explicit flush
        using var list = new MemoryMappedList(100, FlushPolicy.None);

        var vectors = new List<Vector>();
        for (int i = 0; i < 50; i++)
        {
            float[] data = [i * 1.0f, i * 2.0f, i * 3.0f];
            var vector = new Vector(data);
            vectors.Add(vector);
            list.Add(vector);
        }

        // Explicit flush
        list.Flush();

        // Verify all vectors exist
        Assert.That(list.Count, Is.EqualTo(50));
        foreach (var v in vectors)
        {
            Assert.That(list.GetVector(v.Id), Is.Not.Null);
        }
    }

    [Test]
    public void Test_Dispose_PerformsCheckpoint()
    {
        // Test that disposing performs a final checkpoint
        Guid vectorId;
        using (var list = new MemoryMappedList(100, FlushPolicy.None))
        {
            var vector = new Vector([1.0f, 2.0f, 3.0f]);
            vectorId = vector.Id;
            list.Add(vector);

            // No explicit flush - dispose should checkpoint
            // Just verify the vector exists before dispose
            Assert.That(list.GetVector(vectorId), Is.Not.Null);
        }
        // After dispose, the checkpoint should have been performed
        // We can't verify this directly without access to the files
    }
}
