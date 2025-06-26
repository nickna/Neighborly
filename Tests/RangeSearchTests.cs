using Neighborly.Distance;
using Neighborly.Search;
using Neighborly.Tests.Helpers;

namespace Neighborly.Tests;

[TestFixture]
public class RangeSearchTests
{
    private VectorDatabase _db;
    private MockLogger<VectorDatabase> _logger = new MockLogger<VectorDatabase>();

    // Test vectors with known geometric relationships
    private static readonly Vector[] s_testVectors = [
        new Vector([0.0f, 0.0f], "Origin"),
        new Vector([1.0f, 0.0f], "Point (1,0)"),
        new Vector([0.0f, 1.0f], "Point (0,1)"),
        new Vector([1.0f, 1.0f], "Point (1,1)"),
        new Vector([2.0f, 0.0f], "Point (2,0)"),
        new Vector([0.0f, 2.0f], "Point (0,2)"),
        new Vector([3.0f, 4.0f], "Point (3,4)"),
        new Vector([5.0f, 0.0f], "Point (5,0)")
    ];

    [SetUp]
    public void Setup()
    {
        _db?.Dispose();
        _db = new VectorDatabase(_logger, null);
        foreach (var vector in s_testVectors)
        {
            _db.Vectors.Add(vector);
        }
        _db.RebuildSearchIndexesAsync().Wait();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public void LinearRangeSearch_WithRadius1_FindsExpectedVectors()
    {
        // Arrange
        var query = new Vector([0.0f, 0.0f]); // Origin
        var radius = 1.5f; // Should find origin, (1,0), (0,1), and (1,1)

        // Act
        var results = _db.RangeSearch(query, radius, SearchAlgorithm.Linear);

        // Assert
        Assert.That(results.Count, Is.EqualTo(4));
        var resultTexts = results.Select(v => v.OriginalText).ToHashSet();
        Assert.That(resultTexts, Contains.Item("Origin"));
        Assert.That(resultTexts, Contains.Item("Point (1,0)"));
        Assert.That(resultTexts, Contains.Item("Point (0,1)"));
        Assert.That(resultTexts, Contains.Item("Point (1,1)"));
    }

    [Test]
    public void KDTreeRangeSearch_WithRadius1_FindsSameAsLinear()
    {
        // Arrange
        var query = new Vector([0.0f, 0.0f]); // Origin
        var radius = 1.5f;

        // Act
        var linearResults = _db.RangeSearch(query, radius, SearchAlgorithm.Linear);
        var kdTreeResults = _db.RangeSearch(query, radius, SearchAlgorithm.KDTree);

        // Assert
        Assert.That(kdTreeResults.Count, Is.EqualTo(linearResults.Count));
        var linearTexts = linearResults.Select(v => v.OriginalText).ToHashSet();
        var kdTreeTexts = kdTreeResults.Select(v => v.OriginalText).ToHashSet();
        Assert.That(kdTreeTexts, Is.EquivalentTo(linearTexts));
    }

    [Test]
    public void RangeSearch_WithSmallRadius_FindsOnlyNearbyVectors()
    {
        // Arrange
        var query = new Vector([0.0f, 0.0f]); // Origin
        var radius = 0.5f; // Should find only the origin

        // Act
        var results = _db.RangeSearch(query, radius, SearchAlgorithm.Linear);

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].OriginalText, Is.EqualTo("Origin"));
    }

    [Test]
    public void RangeSearch_WithLargeRadius_FindsAllVectors()
    {
        // Arrange
        var query = new Vector([0.0f, 0.0f]); // Origin
        var radius = 10.0f; // Should find all vectors

        // Act
        var results = _db.RangeSearch(query, radius, SearchAlgorithm.Linear);

        // Assert
        Assert.That(results.Count, Is.EqualTo(s_testVectors.Length));
    }

    [Test]
    public void RangeSearch_ResultsAreOrderedByDistance()
    {
        // Arrange
        var query = new Vector([0.0f, 0.0f]); // Origin
        var radius = 5.0f;

        // Act
        var results = _db.RangeSearch(query, radius, SearchAlgorithm.Linear);

        // Assert
        Assert.That(results.Count, Is.GreaterThan(1));
        
        // Check that distances are in ascending order
        var distances = new List<float>();
        for (int i = 0; i < results.Count; i++)
        {
            var distance = query.Distance(results[i]);
            distances.Add(distance);
            
            if (i > 0)
            {
                Assert.That(distance, Is.GreaterThanOrEqualTo(distances[i - 1]),
                    $"Results should be ordered by distance. Vector at index {i} has distance {distance}, " +
                    $"but previous vector had distance {distances[i - 1]}");
            }
        }
    }

    [Test]
    public void RangeSearch_WithCosineDistance_UsesCorrectCalculator()
    {
        // Arrange
        var query = new Vector([1.0f, 0.0f]);
        var radius = 0.5f;
        var cosineCalculator = new CosineSimilarityCalculator();

        // Act
        var results = _db.RangeSearch(query, radius, SearchAlgorithm.Linear, cosineCalculator);

        // Assert
        Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
        
        // Verify that all results are within the cosine distance radius
        foreach (var result in results)
        {
            var distance = cosineCalculator.CalculateDistance(query, result);
            Assert.That(distance, Is.LessThanOrEqualTo(radius),
                $"Vector {result.OriginalText} has cosine distance {distance}, which exceeds radius {radius}");
        }
    }

    [Test]
    public void RangeSearch_WithManhattanDistance_UsesCorrectCalculator()
    {
        // Arrange
        var query = new Vector([1.0f, 1.0f]);
        var radius = 2.0f;
        var manhattanCalculator = new ManhattanDistanceCalculator();

        // Act
        var results = _db.RangeSearch(query, radius, SearchAlgorithm.Linear, manhattanCalculator);

        // Assert
        Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
        
        // Verify that all results are within the Manhattan distance radius
        foreach (var result in results)
        {
            var distance = manhattanCalculator.CalculateDistance(query, result);
            Assert.That(distance, Is.LessThanOrEqualTo(radius),
                $"Vector {result.OriginalText} has Manhattan distance {distance}, which exceeds radius {radius}");
        }
    }

    [Test]
    public void RangeSearch_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var emptyDb = new VectorDatabase(_logger, null);
        var query = new Vector([1.0f, 1.0f]);
        var radius = 1.0f;

        // Act
        var results = emptyDb.RangeSearch(query, radius, SearchAlgorithm.Linear);

        // Assert
        Assert.That(results.Count, Is.EqualTo(0));

        // Cleanup
        emptyDb.Dispose();
    }

    [Test]
    public void RangeSearch_WithStringQuery_ConvertsToEmbedding()
    {
        // Arrange
        var db = new VectorDatabase(_logger, null);
        db.Vectors.Add(new Vector("Hello world"));
        db.Vectors.Add(new Vector("Goodbye world"));
        db.RebuildSearchIndexesAsync().Wait();

        // Act - Use a large radius since embeddings can have large distances
        var results = db.RangeSearch("Hello", 50.0f, SearchAlgorithm.Linear);

        // Assert
        Assert.That(results.Count, Is.GreaterThan(0));

        // Cleanup
        db.Dispose();
    }

    [Test]
    [TestCase(-1.0f)]
    [TestCase(0.0f)]
    public void RangeSearch_WithInvalidRadius_ThrowsArgumentException(float invalidRadius)
    {
        // Arrange
        var query = new Vector([1.0f, 1.0f]);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _db.RangeSearch(query, invalidRadius, SearchAlgorithm.Linear));
    }

    [Test]
    public void RangeSearch_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        Vector? nullQuery = null;
        var radius = 1.0f;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _db.RangeSearch(nullQuery!, radius, SearchAlgorithm.Linear));
    }

    [Test]
    public void RangeSearch_WithNullText_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullText = null;
        var radius = 1.0f;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _db.RangeSearch(nullText!, radius, SearchAlgorithm.Linear));
    }

    [Test]
    public void LinearRangeSearch_StaticMethod_WorksCorrectly()
    {
        // Arrange
        var vectors = new VectorList();
        foreach (var vector in s_testVectors)
        {
            vectors.Add(vector);
        }
        var query = new Vector([0.0f, 0.0f]);
        var radius = 1.5f;

        // Act
        var results = LinearRangeSearch.Search(vectors, query, radius);

        // Assert
        Assert.That(results.Count, Is.EqualTo(4));
        var resultTexts = results.Select(v => v.OriginalText).ToHashSet();
        Assert.That(resultTexts, Contains.Item("Origin"));
        Assert.That(resultTexts, Contains.Item("Point (1,0)"));
        Assert.That(resultTexts, Contains.Item("Point (0,1)"));
        Assert.That(resultTexts, Contains.Item("Point (1,1)"));
    }

    [Test]
    public void RangeSearch_WithUnsupportedAlgorithm_ThrowsNotSupportedException()
    {
        // Arrange
        var query = new Vector([1.0f, 1.0f]);
        var radius = 1.0f;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            _db.RangeSearch(query, radius, SearchAlgorithm.LSH));
    }

    [Test]
    public void RangeSearch_KDTreeVsLinear_PerformanceComparison()
    {
        // Arrange - Create a larger dataset for meaningful performance comparison
        var largeDb = new VectorDatabase(_logger, null);
        var random = new Random(42); // Fixed seed for reproducible results
        
        for (int i = 0; i < 1000; i++)
        {
            var values = new float[10];
            for (int j = 0; j < 10; j++)
            {
                values[j] = (float)(random.NextDouble() * 10);
            }
            largeDb.Vectors.Add(new Vector(values, $"Vector {i}"));
        }
        largeDb.RebuildSearchIndexesAsync().Wait();

        var query = new Vector(Enumerable.Range(0, 10).Select(_ => (float)(random.NextDouble() * 10)).ToArray());
        var radius = 5.0f;

        // Act & Assert - Both should return the same results
        var linearResults = largeDb.RangeSearch(query, radius, SearchAlgorithm.Linear);
        var kdTreeResults = largeDb.RangeSearch(query, radius, SearchAlgorithm.KDTree);

        Assert.That(kdTreeResults.Count, Is.EqualTo(linearResults.Count));
        
        // Verify same vectors are found (order might differ due to floating point precision)
        var linearIds = linearResults.Select(v => v.Id).ToHashSet();
        var kdTreeIds = kdTreeResults.Select(v => v.Id).ToHashSet();
        Assert.That(kdTreeIds, Is.EqualTo(linearIds));

        // Cleanup
        largeDb.Dispose();
    }
}