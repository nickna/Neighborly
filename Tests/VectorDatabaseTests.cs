using Neighborly;

[TestFixture]
public class VectorDatabaseTests
{
    private VectorDatabase _db;

    [SetUp]
    public void Setup()
    {
        _db = new VectorDatabase();
    }

    // Replace all Assert.AreEqual calls with Assert.That calls
    [Test]
    public void TestAdd()
    {
        float[] floatArray = new float[] { 1, 2, 3 };
        byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        var vector = new Vector(byteArray);

        _db.Add(vector);

        Assert.That(_db.Count, Is.EqualTo(1), "Count should be 1 after adding a vector.");
        Assert.IsTrue(_db.Contains(vector), "Database should contain the added vector.");
    }


    [Test]
    public void TestRemove()
    {
        float[] floatArray = new float[] { 1, 2, 3 };
        byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
        var vector = new Vector(byteArray);

        _db.Add(vector);
        Assert.AreEqual(1, _db.Count, "Count should be 1 after adding a vector.");

        _db.Remove(vector);
        Assert.AreEqual(0, _db.Count, "Count should be 0 after removing the vector.");
        Assert.IsFalse(_db.Contains(vector), "Database should not contain the removed vector.");
    }

    [Test]
    public void TestUpdate()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);
        Assert.AreEqual(1, _db.Count, "Count should be 1 after adding a vector.");
        Assert.IsTrue(_db.Contains(vector1), "Database should contain the added vector.");

        _db.Update(vector1, vector2);
        Assert.AreEqual(1, _db.Count, "Count should still be 1 after updating a vector.");
        Assert.IsFalse(_db.Contains(vector1), "Database should not contain the old vector.");
        Assert.IsTrue(_db.Contains(vector2), "Database should contain the new vector.");
    }

    [Test]
    public void TestAddRange()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        List<Vector> vectors = new List<Vector> { vector1, vector2 };

        _db.AddRange(vectors);

        Assert.AreEqual(2, _db.Count, "Count should be 2 after adding two vectors.");
        Assert.IsTrue(_db.Contains(vector1), "Database should contain the first added vector.");
        Assert.IsTrue(_db.Contains(vector2), "Database should contain the second added vector.");
    }


    [Test]
    public void TestRemoveRange()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        List<Vector> vectors = new List<Vector> { vector1, vector2 };

        _db.AddRange(vectors);
        Assert.AreEqual(2, _db.Count, "Count should be 2 after adding two vectors.");

        _db.RemoveRange(vectors);
        Assert.AreEqual(0, _db.Count, "Count should be 0 after removing the vectors.");
        Assert.IsFalse(_db.Contains(vector1), "Database should not contain the first removed vector.");
        Assert.IsFalse(_db.Contains(vector2), "Database should not contain the second removed vector.");
    }

    [Test]
    public void TestContains()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);

        Assert.IsTrue(_db.Contains(vector1), "Database should contain the added vector.");
        Assert.IsFalse(_db.Contains(vector2), "Database should not contain a vector that was not added.");
    }

    [Test]
    public void TestCount()
    {
        Assert.AreEqual(0, _db.Count, "Count should be 0 for an empty database.");

        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        _db.Add(vector1);
        Assert.AreEqual(1, _db.Count, "Count should be 1 after adding a vector.");

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector2);
        Assert.AreEqual(2, _db.Count, "Count should be 2 after adding a second vector.");

        _db.Remove(vector1);
        Assert.AreEqual(1, _db.Count, "Count should be 1 after removing a vector.");
    }

    [Test]
    public void TestClear()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);
        _db.Add(vector2);
        Assert.AreEqual(2, _db.Count, "Count should be 2 after adding two vectors.");

        _db.Clear();
        Assert.AreEqual(0, _db.Count, "Count should be 0 after clearing the database.");
        Assert.IsFalse(_db.Contains(vector1), "Database should not contain the first vector after clearing.");
        Assert.IsFalse(_db.Contains(vector2), "Database should not contain the second vector after clearing.");
    }

    [Test]
    public void TestFind()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);
        _db.Add(vector2);

        var foundVector = _db.Find(v => v.Equals(vector1));
        Assert.AreEqual(vector1, foundVector, "Find should return the correct vector.");

        var notFoundVector = _db.Find(v => v.Equals(new Vector(new byte[] { 7, 8, 9 })));
        Assert.IsNull(notFoundVector, "Find should return null if no vector matches the condition.");
    }

    [Test]
    public void TestFindAll()
    {
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);
        _db.Add(vector2);

        var foundVectors = _db.FindAll(v => v.Equals(vector1) || v.Equals(vector2));
        Assert.AreEqual(2, foundVectors.Count, "FindAll should return all matching vectors.");
        Assert.Contains(vector1, foundVectors, "FindAll should include the first matching vector.");
        Assert.Contains(vector2, foundVectors, "FindAll should include the second matching vector.");

        var notFoundVectors = _db.FindAll(v => v.Equals(new Vector(new byte[] { 7, 8, 9 })));
        Assert.AreEqual(0, notFoundVectors.Count, "FindAll should return an empty list if no vectors match the condition.");
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
        Assert.Throws<ArgumentNullException>(() => _db.Add(null!));
    }

    [TestCase(new float[] { 1, 2, 3 })]
    [TestCase(new float[] { 4, 5, 6 })]
    public void Add_WhenItemIsValid_IncreasesCountByOne(float[] floatArray)
    {
        var vector = CreateVector(floatArray);
        var initialCount = _db.Count;

        _db.Add(vector);

        Assert.AreEqual(initialCount + 1, _db.Count);
        Assert.IsTrue(_db.Contains(vector));
    }
    [Test]
    public void TestSearch()
    {
        // Arrange
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);
        _db.Add(vector2);

        // Act
        var query = CreateVector(new float[] { 2, 3, 4 });
        var result = _db.Search(query, 1);

        // Assert
        Assert.That(result.Count, Is.EqualTo(1), "Search should return the correct number of vectors.");
        Assert.Contains(vector1, result.ToList(), "Search should return the nearest vector.");
    }
    [Test]
    public void TestSaveAndLoad()
    {
        // Arrange
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        _db.Add(vector1);
        _db.Add(vector2);

        var path = Path.GetTempPath();

        // Act
        _db.Save(path);
        _db.Clear();
        _db.Load(path);

        // Assert
        Assert.AreEqual(2, _db.Count, "Count should be 2 after loading the saved database.");
        Assert.IsTrue(_db.Contains(vector1), "Database should contain the first vector after loading.");
        Assert.IsTrue(_db.Contains(vector2), "Database should contain the second vector after loading.");

        // Clean up
        Directory.Delete(path, true);
    }
    [Test]
    public void TestUpdateNonExistentItem()
    {
        // Arrange
        float[] floatArray1 = new float[] { 1, 2, 3 };
        byte[] byteArray1 = new byte[floatArray1.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray1, 0, byteArray1, 0, byteArray1.Length);
        var vector1 = new Vector(byteArray1);

        float[] floatArray2 = new float[] { 4, 5, 6 };
        byte[] byteArray2 = new byte[floatArray2.Length * sizeof(float)];
        Buffer.BlockCopy(floatArray2, 0, byteArray2, 0, byteArray2.Length);
        var vector2 = new Vector(byteArray2);

        // Act
        bool result = _db.Update(vector1, vector2);

        // Assert
        Assert.IsFalse(result, "Update should return false when the old item does not exist.");
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
                    float[] floatArray = new float[] { j, j + 1, j + 2 };
                    byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
                    Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
                    var vector = new Vector(byteArray);
                    _db.Add(vector);
                }
            });
            threads[i].Start();
        }

        for (int i = 0; i < threadCount; i++)
        {
            threads[i].Join();
        }

        // Assert
        Assert.AreEqual(threadCount * vectorsPerThread, _db.Count, "Count should be correct after concurrent additions.");
    }


}
