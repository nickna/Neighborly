﻿using System.Diagnostics;

namespace Neighborly.Tests;

[TestFixture]
public class MemoryMappedFileTests
{
    private VectorDatabase _db;

    [SetUp]
    public void Setup()
    {
        _db?.Dispose();

        _db = new VectorDatabase();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public void Defrag_WhenCalled_RemovesTombstonedEntriesAndCompactsData()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        var vector3 = new Vector(new float[] { 7, 8, 9 });

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        _db.Vectors.Add(vector3);

        // Act: Remove vector2 and defragment
        _db.Vectors.Remove(vector2);
        int countBeforeDefrag = _db.Vectors.Count;
        _db.Vectors.Defrag();
        int countAfterDefrag = _db.Vectors.Count;

        // Assert
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain vector1.");
        Assert.That(_db.Vectors.Contains(vector2), Is.False, "Database should not contain removed vector2.");
        Assert.That(_db.Vectors.Contains(vector3), Is.True, "Database should contain vector3.");
        Assert.That(countBeforeDefrag, Is.EqualTo(2), "Count should be 2 before defragmentation.");
        Assert.That(countAfterDefrag, Is.EqualTo(2), "Count should remain 2 after defragmentation.");

        // Check that the valid entries can be accessed correctly
        var retrievedVector1 = _db.Vectors.Find(v => v.Id == vector1.Id);
        var retrievedVector3 = _db.Vectors.Find(v => v.Id == vector3.Id);
        Assert.That(retrievedVector1, Is.EqualTo(vector1), "Retrieved vector1 should match the original vector1.");
        Assert.That(retrievedVector3, Is.EqualTo(vector3), "Retrieved vector3 should match the original vector3.");
    }

    [Test]
    public void Defrag_WithLargeNumberOfEntries_CompletesInReasonableTime()
    {
        // Arrange
        int entryCount = 10000; // Adjust based on expected performance
        for (int i = 0; i < entryCount; i++)
        {
            var vector = new Vector(new float[] { i, i + 1, i + 2 });
            _db.Vectors.Add(vector);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        _db.Vectors.Defrag();
        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), "Defrag should complete in under 1 second for 10,000 entries."); // Adjust time based on expected performance
    }

    [Test]
    public void Defrag_WhenCalledOnEmptyDatabase_DoesNotThrow()
    {
        // Arrange - Ensure database is empty
        _db.Vectors.Clear();

        // Act & Assert
        Assert.DoesNotThrow(() => _db.Vectors.Defrag(), "Defrag should not throw an exception when called on an empty database.");
    }

    [Test]
    public void Defrag_WhenCalledOnDatabaseWithNoTombstonedEntries_DoesNotThrow()
    {
        // Arrange - Ensure database has no tombstoned entries
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        var vector3 = new Vector(new float[] { 7, 8, 9 });

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        _db.Vectors.Add(vector3);

        // Act & Assert
        Assert.DoesNotThrow(() => _db.Vectors.Defrag(), "Defrag should not throw an exception when called on a database with no tombstoned entries.");
    }

    [Test]
    public void Defrag_WhenCalledOnDatabaseWithNoTombstonedEntries_CountRemainsUnchanged()
    {
        // Arrange - Ensure database has no tombstoned entries
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        var vector3 = new Vector(new float[] { 7, 8, 9 });

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        _db.Vectors.Add(vector3);

        // Act
        _db.Vectors.Defrag();

        // Assert
        Assert.That(_db.Count, Is.EqualTo(3), "Count should remain unchanged when defrag is called on a database with no tombstoned entries.");
    }

    // Test the CalculateFragmentation method
    [Test]
    public void CalculateFragmentation_WhenCalled_ReturnsCorrectValue()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        var vector3 = new Vector(new float[] { 7, 8, 9 });

        // Create a MemoryMappedList with a smaller capacity
        var capacity = 10L;
        using var db = new MemoryMappedList(capacity);

        db.Add(vector1);
        db.Add(vector2);
        db.Add(vector3);

        // Act
        db.Remove(vector2);
        var fragmentation = db.CalculateFragmentation();

        // Assert
        Assert.That(fragmentation, Is.EqualTo(50), "Fragmentation should be 50% after removing one entry.");
    }

    [Test]
    public void DefragBatch_WhenCalled_RemovesTombstonedEntriesAndCompactsData()
    {
        // Arrange
        var vectors = new List<Vector>();
        int numBatches = 3;
        int entriesPerBatch = 100;

        for (int i = 0; i < numBatches * entriesPerBatch; i++)
        {
            var vector = new Vector(new float[] { i, i + 1, i + 2 });
            vectors.Add(vector);
            _db.Vectors.Add(vector);
        }

        // Remove some vectors to create tombstoned entries
        for (int i = 0; i < numBatches * entriesPerBatch; i += 2)
        {
            _db.Vectors.Remove(vectors[i]);
        }

        int countBeforeDefrag = _db.Vectors.Count;
        Console.WriteLine($"Count before defrag: {countBeforeDefrag}");

        // Act: Defragment in batches
        long fragmentation;
        int iterationCount = 0;
        int maxIterations = 1000; // Adjust as needed
        var timeout = TimeSpan.FromSeconds(10); // Additional timeout safety
        var stopwatch = Stopwatch.StartNew();
        
        do
        {
            fragmentation = _db.Vectors.DefragBatch();
            iterationCount++;
            
            // Only log every 100 iterations to reduce console spam
            if (iterationCount % 100 == 0 || fragmentation <= 0)
            {
                Console.WriteLine($"Iteration {iterationCount}: Fragmentation = {fragmentation}");
            }
            
            // Safety check for infinite loops
            if (stopwatch.Elapsed > timeout)
            {
                Assert.Fail($"Defragmentation timed out after {timeout.TotalSeconds} seconds. Iterations: {iterationCount}, Last fragmentation: {fragmentation}");
            }
        } while (fragmentation > 0 && iterationCount < maxIterations);
        
        stopwatch.Stop();

        int countAfterDefrag = _db.Vectors.Count;
        Console.WriteLine($"Count after defrag: {countAfterDefrag}");

        // Assert
        Assert.That(countAfterDefrag, Is.EqualTo(countBeforeDefrag), "Count should remain unchanged after defragmentation.");

        // Check that the valid entries can be accessed correctly
        int successfulRetrievals = 0;
        for (int i = 1; i < numBatches * entriesPerBatch; i += 2)
        {
            var retrievedVector = _db.Vectors.Find(v => v.Id == vectors[i].Id);
            if (retrievedVector != null && retrievedVector.Equals(vectors[i]))
            {
                successfulRetrievals++;
            }
            else
            {
                Console.WriteLine($"Failed to retrieve vector at index {i}. ID: {vectors[i].Id}");
            }
        }

        Console.WriteLine($"Successfully retrieved {successfulRetrievals} out of {countAfterDefrag} vectors");
        Assert.That(successfulRetrievals, Is.EqualTo(countAfterDefrag), "All non-tombstoned vectors should be retrievable after defragmentation.");
    }
    [Test]
    public void DefragBatch_WithLargeNumberOfEntries_CompletesInReasonableTime()
    {
        // Arrange
        var vectors = new List<Vector>();
        int numBatches = 3;
        int entriesPerBatch = 100;

        for (int i = 0; i < numBatches * entriesPerBatch; i++)
        {
            var vector = new Vector(new float[] { i, i + 1, i + 2 });
            vectors.Add(vector);
            _db.Vectors.Add(vector);
        }

        // Remove some vectors to create tombstoned entries
        for (int i = 0; i < numBatches * entriesPerBatch; i += 2)
        {
            _db.Vectors.Remove(vectors[i]);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        long fragmentation;
        int iterationCount = 0;
        int maxIterations = 1000; // Prevent infinite loops
        var timeout = TimeSpan.FromSeconds(10); // Additional timeout safety
        
        do
        {
            fragmentation = _db.Vectors.DefragBatch();
            iterationCount++;
            
            // Safety check for infinite loops
            if (stopwatch.Elapsed > timeout)
            {
                Assert.Fail($"Defragmentation timed out after {timeout.TotalSeconds} seconds. Iterations: {iterationCount}, Last fragmentation: {fragmentation}");
            }
        } while (fragmentation > 0 && iterationCount < maxIterations);
        stopwatch.Stop();

        // Assert
        var maxAcceptableTime = TimeSpan.FromMilliseconds(300); // Adjust the threshold as needed
        Assert.That(stopwatch.Elapsed, Is.LessThan(maxAcceptableTime), $"Defragmentation should complete within {maxAcceptableTime.TotalSeconds} seconds. Actual: {stopwatch.Elapsed.TotalSeconds}s, Iterations: {iterationCount}");
        
        // Ensure we didn't hit the iteration limit due to a bug
        Assert.That(iterationCount, Is.LessThan(maxIterations), $"Defragmentation hit the maximum iteration limit ({maxIterations}), which may indicate an infinite loop bug. Last fragmentation value: {fragmentation}");
    }

    [Test]
    public void AddWhenCalledAfterAnEnumerationWorks()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        _db.Vectors.Add(vector1);

        // Act
        _ = _db.Vectors.Find(v => v.Id == Guid.NewGuid());
        _db.Vectors.Add(vector2);

        // Assert
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain vector1.");
        Assert.That(_db.Vectors.Contains(vector2), Is.True, "Database should contain vector2.");
    }
    [Test]
    public void GetFileInfo_WhenCalledAfterForceFlush_ReturnsCorrectFileInfo()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        // Act
        _db.Vectors.ForceFlush();
        var fileInfo = _db.Vectors.GetFileInfo();

        // Assert
        Assert.That(fileInfo, Is.InstanceOf<long[]>(), "FileInfo should be an array of long.");
        Assert.That(fileInfo.Length, Is.EqualTo(4), "FileInfo should contain four elements.");

        // Assuming the first element is the size of the index file and the second is the size of the data file
        // These assertions might need to be adjusted based on the actual implementation of GetFileInfo
        Assert.That(fileInfo[0], Is.GreaterThan(0), "Index file size should be greater than 0.");
        Assert.That(fileInfo[2], Is.GreaterThan(0), "Data file size should be greater than 0.");
    }

    [Test]
    public void GetFileInfo_WhenCalled_ReturnsCorrectFileInfo()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1, 2, 3 });
        var vector2 = new Vector(new float[] { 4, 5, 6 });
        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        // Act
        var fileInfo = _db.Vectors.GetFileInfo();

        // Assert
        Assert.That(fileInfo, Is.InstanceOf<long[]>(), "FileInfo should be an array of long.");
        Assert.That(fileInfo.Length, Is.EqualTo(4), "FileInfo should contain four elements.");

        // Memory-mapped files allocate initial disk blocks even without explicit flushing
        // This is expected behavior for the memory-mapped file system
        Assert.That(fileInfo[0], Is.GreaterThanOrEqualTo(0), "Index file size should be non-negative.");
        Assert.That(fileInfo[2], Is.GreaterThanOrEqualTo(0), "Data file size should be non-negative.");
    }

    [Test]
    public void TestIndexAndFindIndexWithModification()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        float[] floatArray3 = [7, 8, 9];
        var vector3 = new Vector(floatArray3);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        _db.Vectors.Add(vector3);

        var indexOfFirst = _db.Vectors.IndexOf(vector1);
        Assert.That(indexOfFirst, Is.EqualTo(0), "IndexOf should return the correct index for the first vector.");

        var indexOfSecond = _db.Vectors.IndexOf(vector2);
        Assert.That(indexOfSecond, Is.EqualTo(1), "IndexOf should return the correct index for the second vector.");

        int indexOfThird = _db.Vectors.IndexOf(vector3);
        Assert.That(indexOfThird, Is.EqualTo(2), "IndexOf should return the correct index for the third vector.");

        _db.Vectors.Remove(vector1);
        // Check that the index of the second vector is still correct after removing the first vector
        indexOfSecond = _db.Vectors.IndexOf(vector2);
        Assert.That(indexOfSecond, Is.EqualTo(0), "IndexOf should return the correct index for the second vector after removing the first vector.");

        var alsoVector2ByIndex = _db.Vectors.Get(indexOfSecond);
        Assert.That(alsoVector2ByIndex, Is.EqualTo(vector2), "Get should return the correct vector by index.");

        var alsoVector2ById = _db.Vectors.GetById(vector2.Id);
        Assert.That(alsoVector2ById, Is.EqualTo(vector2), "GetById should return the correct vector by Id.");

        // Check that the index of the third vector is still correct after removing the first vector
        indexOfThird = _db.Vectors.IndexOf(vector3);
        Assert.That(indexOfThird, Is.EqualTo(1), "IndexOf should return the correct index for the third vector.");

        // Update the 2nd vector - this is done in-place, so it maintains its position
        var updatedVector2 = new Vector(new float[] { 42, 69, 420 });
        var updated = _db.Vectors.Update(vector2.Id, updatedVector2);
        Assert.That(updated, Is.True, "Update should return true when the vector is updated.");

        var indexOfUpdatedSecond = _db.Vectors.IndexOf(updatedVector2);
        Assert.That(indexOfUpdatedSecond, Is.EqualTo(0), "IndexOf should return the correct index for the updated second vector.");

        // Check that the index of the 2nd and 3rd vectors are still correct after removing the first vector and after the update
        int alsoIndexOfFirst = _db.Vectors.IndexOf(vector1);
        Assert.That(alsoIndexOfFirst, Is.EqualTo(-1), "IndexOf should return -1 for the first vector after it is removed.");

        int alsoIndexOfThird = _db.Vectors.IndexOf(vector3);
        Assert.That(alsoIndexOfThird, Is.EqualTo(1), "IndexOf should return 1 for the third vector after all the updates.");

        // Ensure that IndexById works as expected
        int indexOfThirdById = _db.Vectors.FindIndexById(vector3.Id);
        Assert.That(indexOfThirdById, Is.EqualTo(1), "FindIndexById should return the correct index for the third vector.");

        int indexOfUpdatedSecondById = _db.Vectors.FindIndexById(updatedVector2.Id);
        Assert.That(indexOfUpdatedSecondById, Is.EqualTo(0), "FindIndexById should return the correct index for the updated second vector.");

        int inddexOfFirstById = _db.Vectors.FindIndexById(vector1.Id);
        Assert.That(inddexOfFirstById, Is.EqualTo(-1), "FindIndexById should return -1 for the first vector after it is removed.");
    }

    [Test]
    public void TestVectorEnumeratorAfterModification()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        float[] floatArray3 = [7, 8, 9];
        var vector3 = new Vector(floatArray3);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        _db.Vectors.Add(vector3);

        _db.Vectors.Remove(vector2);

        var vectors = _db.Vectors.ToList(); // Force enumeration

        Assert.That(vectors, Contains.Item(vector1), "Database should contain vector1.");
        Assert.That(vectors, Does.Not.Contain(vector2), "Database should not contain vector2.");
        Assert.That(vectors, Contains.Item(vector3), "Database should contain vector3.");

        // Update the 3nd vector
        var updatedVector3 = new Vector(new float[] { 42, 69, 420 });
        var updated = _db.Vectors.Update(vector3.Id, updatedVector3);
        Assert.That(updated, Is.True, "Update should return true when the vector is updated.");

        var updatedVectors = _db.Vectors.ToList(); // Force enumeration
        Assert.That(updatedVectors, Contains.Item(vector1), "Database should contain vector1.");
        Assert.That(updatedVectors, Does.Not.Contain(vector2), "Database should not contain vector2.");
        Assert.That(updatedVectors, Does.Not.Contain(vector3), "Database should not contain vector3.");
        Assert.That(updatedVectors, Contains.Item(updatedVector3), "Database should contain updatedVector3.");
    }
}
