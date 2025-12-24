using Microsoft.Extensions.Logging;
using Neighborly;
using Neighborly.Search;
using Neighborly.Tests.Helpers;

namespace Neighborly.Tests;
[TestFixture]
public class VectorDatabaseTests
{
    private VectorDatabase _db;
    private MockLogger<VectorDatabase> _logger = new MockLogger<VectorDatabase>();

    [SetUp]
    public void Setup()
    {
        _db?.Dispose();
        _db = new VectorDatabase(_logger, null);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    // Replace all Assert.AreEqual calls with Assert.That calls
    [Test]
    public void TestAdd()
    {
        float[] floatArray = [1, 2, 3];
        var vector = new Vector(floatArray);

        _db.Vectors.Add(vector);

        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after adding a vector.");
        Assert.That(_db.Vectors.Contains(vector), Is.True,  "Database should contain the added vector.");
    }


    [Test]
    public void TestRemove()
    {
        float[] floatArray = [1, 2, 3];
        var vector = new Vector(floatArray);

        _db.Vectors.Add(vector);
        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after adding a vector.");

        _db.Vectors.Remove(vector);
        Assert.That(_db.Count, Is.EqualTo(0), "Count should be 0 after removing the vector.");
        Assert.That(_db.Vectors.Contains(vector), Is.False, "Database should not contain the removed vector.");
    }

    [Test]
    public void TestUpdate()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after adding a vector.");
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain the added vector.");

        var updated = _db.Vectors.Update(vector1.Id, vector2);
        Assert.That(updated, Is.True, "Update should return true when the old item exists.");
        Assert.That(_db.Count, Is.EqualTo(1), "Count should still be 1 after updating a vector.");
        var vectorFromDb = _db.Vectors.GetById(vector1.Id);
        Assert.That(vectorFromDb, Is.EqualTo(vector2), "The vector should be updated in the database.");
        Assert.That(vectorFromDb, Is.Not.EqualTo(vector1), "The vector should not be the same as the old vector.");
    }

    [Test]
    public void TestAddRange()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        List<Vector> vectors = new List<Vector> { vector1, vector2 };

        _db.Vectors.AddRange(vectors);

        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after adding two vectors.");
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain the first added vector.");
        Assert.That(_db.Vectors.Contains(vector2), Is.True, "Database should contain the second added vector.");
    }


    [Test]
    public void TestRemoveRange()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        List<Vector> vectors = new List<Vector> { vector1, vector2 };

        _db.Vectors.AddRange(vectors);
        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after adding two vectors.");

        _db.Vectors.RemoveRange(vectors);
        Assert.That(_db.Count, Is.EqualTo(0), "Count should be 0 after removing the vectors.");
        Assert.That(_db.Vectors.Contains(vector1), Is.False, "Database should not contain the first removed vector.");
        Assert.That(_db.Vectors.Contains(vector2), Is.False, "Database should not contain the second removed vector.");
    }

    [Test]
    public void TestContains()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);

        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain the added vector.");
        Assert.That(_db.Vectors.Contains(vector2), Is.False, "Database should not contain a vector that was not added.");
    }

    [Test]
    public void TestCount()
    {
        Assert.That(_db.Count, Is.EqualTo(0), "Count should be 0 for an empty database.");

        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        _db.Vectors.Add(vector1);
        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after adding a vector.");

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector2);
        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after adding a second vector.");

        _db.Vectors.Remove(vector1);
        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after removing a vector.");
    }

    [Test]
    public void TestClear()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after adding two vectors.");

        _db.Vectors.Clear();
        Assert.That(_db.Count, Is.EqualTo(0), "Count should be 0 after clearing the database.");
        Assert.That(_db.Vectors.Contains(vector1), Is.False, "Database should not contain the first vector after clearing.");
        Assert.That(_db.Vectors.Contains(vector2), Is.False, "Database should not contain the second vector after clearing.");
    }

    [Test]
    public void TestFind()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        var foundVector = _db.Vectors.Find(v => v.Equals(vector1));
        Assert.That(foundVector, Is.EqualTo(vector1), "Find should return the correct vector.");

        var notFoundVector = _db.Vectors.Find(v => v.Equals(new Vector([7f, 8, 9])));
        Assert.That(notFoundVector, Is.Null, "Find should return null if no vector matches the condition.");
    }

    [Test]
    public void TestFindAll()
    {
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        var foundVectors = _db.Vectors.FindAll(v => v.Equals(vector1) || v.Equals(vector2));
        Assert.That(foundVectors.Count, Is.EqualTo(2), "FindAll should return all matching vectors.");
        Assert.That(foundVectors, Does.Contain(vector1), "FindAll should include the first matching vector.");
        Assert.That(foundVectors, Does.Contain(vector2), "FindAll should include the second matching vector.");

        var notFoundVectors = _db.Vectors.FindAll(v => v.Equals(new Vector([7f, 8, 9])));
        Assert.That(notFoundVectors.Count, Is.EqualTo(0), "FindAll should return an empty list if no vectors match the condition.");
    }

    private Vector CreateVector(float[] floatArray)
    {
        byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        return new Vector(byteArray);
    }

    [Test]
    public void Add_WhenItemIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _db.Vectors.Add(null!));
    }

    [TestCase(new float[] { 1, 2, 3 })]
    [TestCase(new float[] { 4, 5, 6 })]
    public void Add_WhenItemIsValid_IncreasesCountByOne(float[] floatArray)
    {
        var vector = new Vector(floatArray);
        var initialCount = _db.Count;

        _db.Vectors.Add(vector);

        Assert.That(_db.Count, Is.EqualTo(initialCount + 1));
        Assert.That(_db.Vectors.Contains(vector), Is.True);
    }
    [Test]
    public async Task TestSearch([Values(SearchAlgorithm.BallTree, SearchAlgorithm.KDTree)] SearchAlgorithm searchAlgorithm, [Values(1, 2)] int matchingVectors)
    {
        // Arrange
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        await _db.RebuildSearchIndexesAsync().ConfigureAwait(false);

        // Act
        var query = new Vector([2f, 3f, 4f]);
        var result = _db.Search(query, matchingVectors, searchAlgorithm, 3.5f); // similarityThreshold is set loose to allow partial matches

        // Assert
        Assert.That(result, Has.Count.EqualTo(matchingVectors), "Search should return the correct number of vectors.");
        Assert.That(result, Does.Contain(vector1), "Search should return the nearest vector.");
    }
    [Test]
    public async Task TestExactMatchSearch([Values(SearchAlgorithm.Linear, SearchAlgorithm.LSH)] SearchAlgorithm searchAlgorithm)
    {
        // Arrange
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);
        await _db.RebuildSearchIndexesAsync().ConfigureAwait(false);

        // Act
        var query = new Vector([1f, 2f, 3f]);
        var result = _db.Search(query, 1, searchAlgorithm);

        // Assert

        Assert.That(result.Count, Is.EqualTo(1), "Search should return the correct number of vectors.");
        Assert.That(result.Contains(vector1), Is.True,  "Search should return the nearest vector.");

    }
    [Test]
    public async Task TestSaveAndLoad()
    {
        // Arrange
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        await _db.RebuildSearchIndexesAsync().ConfigureAwait(false);

        var path = Path.Combine(Path.GetTempPath(), $"test_save_{Guid.NewGuid()}");

        // Act
        await _db.SaveAsync(path).ConfigureAwait(false);
        _db.Vectors.Clear();
        await _db.LoadAsync(path).ConfigureAwait(false);

        // Assert
        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after loading the saved database.");
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain the first vector after loading.");
        Assert.That(_db.Vectors.Contains(vector2), Is.True, "Database should contain the second vector after loading.");

        // Clean up
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    [Test]
    [Ignore("Test isolation issue - functionality verified by TestSaveAndLoad and HNSW_Serialization_PreservesSearchCapability")]
    public async Task LoadAsync_VerifyAtomicSwapPattern()
    {
        // Verify the two-phase load pattern works correctly
        // Uses shared _db fixture like other tests

        // Arrange
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        var path = Path.Combine(Path.GetTempPath(), $"test_atomic_load_{Guid.NewGuid()}");

        // Act - save the database
        await _db.SaveAsync(path).ConfigureAwait(false);

        // The new LoadAsync uses atomic swap, so clear is not needed
        // but we test that Load replaces existing data correctly
        _db.Vectors.Clear();
        Assert.That(_db.Count, Is.EqualTo(0), "Count should be 0 after clearing.");

        // Load using the new two-phase atomic swap pattern
        await _db.LoadAsync(path).ConfigureAwait(false);

        // Assert
        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after loading with atomic swap.");
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain the first vector.");
        Assert.That(_db.Vectors.Contains(vector2), Is.True, "Database should contain the second vector.");

        // Clean up
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    [Test]
    public void TestUpdateNonExistentItem()
    {
        // Arrange
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        // Act
        bool result = _db.Vectors.Update(vector2.Id, vector2);

        // Assert
        Assert.That(result, Is.False, "Update should return false when the old item does not exist.");
    }
    [Test]
    public void TestConcurrency()
    {
        // Arrange
        int threadCount = 10;
        int vectorsPerThread = 1000;
        var threads = new Thread[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < vectorsPerThread; j++)
                {
                    float[] floatArray = [j, j + 1, j + 2];
                    var vector = new Vector(floatArray);
                    _db.Vectors.Add(vector);
                }
            });
            threads[i].Start();
        }

        for (int i = 0; i < threadCount; i++)
        {
            threads[i].Join();
        }

        // Assert
        Assert.That(_db.Count, Is.EqualTo(threadCount * vectorsPerThread), "Count should be correct after concurrent additions.");
    }

    [Test]
    public void Search_WithInvalidK_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var query = new Vector([1f, 2f, 3f]);
        var k = -1;

        // Act & Assert - validation now throws before reaching internal try-catch
        Assert.Throws<ArgumentOutOfRangeException>(() => _db.Search(query, k));
    }

    [Test]
    public void Ctor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VectorDatabase(null!, null));
    }

    [Test]
    public void SaveAsync_WithNullPath_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(() => _db.SaveAsync(null!));
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    public void SaveAsync_WithEmptyOrWhitespacePath_ThrowsArgumentException(string path)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _db.SaveAsync(path));
    }

    [Test]
    public void LoadAsync_WithNullPath_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(() => _db.LoadAsync(null!));
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    public void LoadAsync_WithEmptyOrWhitespacePath_ThrowsArgumentException(string path)
    {
        Assert.ThrowsAsync<ArgumentException>(() => _db.LoadAsync(path));
    }

    [Test]
    [Ignore("Requires Ollama running locally. Not suitable for automated builds.")]
    public void Search_ChangeEmbeddingFactory()
    {
        // Arrange
        var embeddingFactory = new EmbeddingGenerationInfo { Source = EmbeddingSource.Ollama };
        _db.SetEmbeddingGenerationInfo(embeddingFactory);

        // Act
        Vector v = _db.GenerateVector(originalText: "Hello, World!");

        // Assert
        Assert.That(v, Is.Not.Null);
        Assert.That(v.OriginalText, Is.EqualTo("Hello, World!"));

    }

    #region Background Indexing Service Integration Tests

    [Test]
    public void Constructor_WithDefaultOptions_UsesDefaultBehavior()
    {
        // This test verifies backward compatibility - existing behavior should work
        using var db = new VectorDatabase(_logger, null);

        Assert.That(db, Is.Not.Null);
        Assert.That(db.Count, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_WithCustomOptions_AcceptsConfiguration()
    {
        // Arrange
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromSeconds(1),
            CheckInterval = TimeSpan.FromSeconds(1)
        };

        // Act
        using var db = new VectorDatabase(_logger, null, options);

        // Assert
        Assert.That(db, Is.Not.Null);
    }

    [Test]
    public void Constructor_WithDisabledOptions_DoesNotFail()
    {
        // Arrange
        var options = BackgroundIndexServiceOptions.Disabled();

        // Act
        using var db = new VectorDatabase(_logger, null, options);

        // Assert
        Assert.That(db, Is.Not.Null);
        Assert.That(db.Count, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_WithInvalidOptions_Throws()
    {
        // Arrange
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMilliseconds(50) // Too short, should fail validation
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VectorDatabase(_logger, null, options));
    }

    [Test]
    public async Task AddVector_TriggersBackgroundIndexRebuild()
    {
        // Arrange
        var options = new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromMilliseconds(500),
            CheckInterval = TimeSpan.FromMilliseconds(200)
        };

        using var db = new VectorDatabase(_logger, null, options);

        // Act
        float[] floatArray = [1, 2, 3];
        var vector = new Vector(floatArray);
        db.Vectors.Add(vector);

        // Wait for background rebuild to occur
        await Task.Delay(1000);

        // Assert - verify database still works correctly
        Assert.That(db.Count, Is.EqualTo(1));
        Assert.That(db.Vectors.Contains(vector), Is.True);
    }

    #endregion
}
