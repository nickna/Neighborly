using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
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
        _db.Vectors.Clear();
        float[] floatArray = [1, 2, 3];
        var vector = new Vector(floatArray);

        _db.Vectors.Add(vector);

        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after adding a vector.");
        Assert.That(_db.Vectors.Contains(vector), Is.True,  "Database should contain the added vector.");
    }


    [Test]
    public void TestRemove()
    {
        _db.Vectors.Clear();
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
        _db.Vectors.Clear();
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
        _db.Vectors.Clear();
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
        _db.Vectors.Clear();
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
        _db.Vectors.Clear();
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
        _db.Vectors.Clear();
        float[] floatArray1 = [1, 2, 3];
        var vector1 = new Vector(floatArray1);

        float[] floatArray2 = [4, 5, 6];
        var vector2 = new Vector(floatArray2);

        _db.Vectors.Add(vector1);
        _db.Vectors.Add(vector2);

        await _db.RebuildSearchIndexesAsync().ConfigureAwait(true);

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
        await _db.RebuildSearchIndexesAsync().ConfigureAwait(true);

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

        await _db.RebuildSearchIndexesAsync().ConfigureAwait(true);

        var path = Path.GetTempPath();

        // Act    
        await _db.SaveAsync(path).ConfigureAwait(true);
        _db.Vectors.Clear();
        await _db.LoadAsync(path).ConfigureAwait(true);

        // Assert
        Assert.That(_db.Count, Is.EqualTo(2), "Count should be 2 after loading the saved database.");
        Assert.That(_db.Vectors.Contains(vector1), Is.True, "Database should contain the first vector after loading.");
        Assert.That(_db.Vectors.Contains(vector2), Is.True, "Database should contain the second vector after loading.");


        // Clean up
        string filePath = Path.Combine(path, "vectors.bin");
        File.Delete(filePath);
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
    public void Search_WhenSearchMethodThrowsAnException_ExceptionIsLogged()
    {
        // Arrange
        // _logger is already attached to the VectorDatabase instance

        var query = new Vector([1f, 2f, 3f]);
        var k = -1;

        // Act
        _db.Search(query, k);

        // Assert
        //Assert.That(_logger.LastLogLevel, Is.EqualTo(LogLevel.Error), "An error should be logged.");
        //Assert.That(_logger.LastEventId?.Id, Is.EqualTo(0), "The event ID should be 0.");
        //if (_logger.LastState is IReadOnlyList<KeyValuePair<string, object?>> state)
        //{
        //    Assert.That(state, Contains.Item(new KeyValuePair<string, object?>("Query", query)), "The query should be logged.");
        //    Assert.That(state, Contains.Item(new KeyValuePair<string, object?>("k", k)), "The number of neighbors should be logged.");
        //    Assert.That(state, Contains.Item(new KeyValuePair<string, object?>("{OriginalFormat}", "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).")), "The message template should be logged.");
        //}

        Assert.That(_logger.LastException, Is.InstanceOf<System.ArgumentOutOfRangeException>(), "The exception should be logged.");
        Assert.That(_logger.LastMessage, Is.EqualTo("Could not find vector `Neighborly.Vector` in the database searching the -1 nearest neighbor(s)."), "The message should be correct.");
    }

    [Test]
    public void Ctor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VectorDatabase(null!, null));
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
}
