using NUnit.Framework;
using Neighborly;
using Neighborly.Distance;
using Neighborly.Search;
using System;
using System.Linq;

namespace Tests;

[TestFixture]
public class BatchDistanceCalculationTests
{
    private readonly Random _random = new(42);

    private float[] GenerateRandomVector(int dimension)
    {
        float[] values = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            values[i] = (float)(_random.NextDouble() * 2 - 1);
        }
        return values;
    }

    [Test]
    public void BatchEuclideanDistance_ProducesCorrectResults()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 100; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(128)));
        }
        var query = new Vector(GenerateRandomVector(128));

        var singleCalculator = new EuclideanDistanceCalculator();
        var batchCalculator = BatchEuclideanDistanceCalculator.Instance;

        // Act
        // Calculate distances one by one
        float[] singleResults = new float[vectors.Count];
        for (int i = 0; i < vectors.Count; i++)
        {
            singleResults[i] = singleCalculator.CalculateDistance(query, vectors[i]);
        }

        // Calculate distances in batch
        float[] batchResults = batchCalculator.CalculateDistances(query, vectors);

        // Assert
        Assert.That(batchResults.Length, Is.EqualTo(singleResults.Length));
        for (int i = 0; i < singleResults.Length; i++)
        {
            Assert.That(batchResults[i], Is.EqualTo(singleResults[i]).Within(1e-5f),
                $"Distance mismatch at index {i}");
        }
    }

    [Test]
    public void BatchCosineSimilarity_ProducesCorrectResults()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 50; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(64)));
        }
        var query = new Vector(GenerateRandomVector(64));

        var singleCalculator = new CosineSimilarityCalculator();
        var batchCalculator = BatchCosineSimilarityCalculator.Instance;

        // Act
        float[] singleResults = new float[vectors.Count];
        for (int i = 0; i < vectors.Count; i++)
        {
            singleResults[i] = singleCalculator.CalculateDistance(query, vectors[i]);
        }

        float[] batchResults = batchCalculator.CalculateDistances(query, vectors);

        // Assert
        Assert.That(batchResults.Length, Is.EqualTo(singleResults.Length));
        for (int i = 0; i < singleResults.Length; i++)
        {
            Assert.That(batchResults[i], Is.EqualTo(singleResults[i]).Within(1e-5f),
                $"Similarity mismatch at index {i}");
        }
    }

    [Test]
    public void BatchDistance_ExtensionMethod_Works()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 20; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(32)));
        }
        var query = new Vector(GenerateRandomVector(32));

        // Act
        float[] batchResults = query.BatchDistance(vectors);

        // Assert
        Assert.That(batchResults.Length, Is.EqualTo(vectors.Count));
        
        // Verify results match individual calculations
        for (int i = 0; i < vectors.Count; i++)
        {
            float expected = query.Distance(vectors[i]);
            Assert.That(batchResults[i], Is.EqualTo(expected).Within(1e-5f));
        }
    }

    [Test]
    public void BatchOptimizedLinearSearch_ProducesCorrectResults()
    {
        // Arrange
        var vectorList = new VectorList();
        var vectors = new List<Vector>();
        
        for (int i = 0; i < 100; i++)
        {
            var vector = new Vector(GenerateRandomVector(64));
            vectorList.Add(vector);
            vectors.Add(vector);
        }
        
        var query = new Vector(GenerateRandomVector(64));
        int k = 10;

        // Act
        var originalResults = LinearSearch.Search(vectorList, query, k);
        
        var batchSearch = new BatchOptimizedLinearSearch();
        var batchResults = batchSearch.Search(vectorList, query, k);

        // Assert
        Assert.That(batchResults.Count, Is.EqualTo(originalResults.Count));
        Assert.That(batchResults.Count, Is.EqualTo(k));
        
        // Both should return the same vectors (though order might differ slightly due to floating point)
        var originalIds = originalResults.Select(v => v.Id).ToHashSet();
        var batchIds = batchResults.Select(v => v.Id).ToHashSet();
        
        Assert.That(batchIds.SetEquals(originalIds), Is.True,
            "Batch search should return the same vectors as original search");
    }

    [Test]
    public void BatchOptimizedRangeSearch_ProducesCorrectResults()
    {
        // Arrange
        var vectorList = new VectorList();
        
        for (int i = 0; i < 50; i++)
        {
            vectorList.Add(new Vector(GenerateRandomVector(32)));
        }
        
        var query = new Vector(GenerateRandomVector(32));
        float radius = 5.0f;

        // Act
        var originalResults = LinearRangeSearch.Search(vectorList, query, radius);
        var batchResults = BatchOptimizedLinearRangeSearch.Search(vectorList, query, radius);

        // Assert
        Assert.That(batchResults.Count, Is.EqualTo(originalResults.Count));
        
        // Both should return the same vectors
        var originalIds = originalResults.Select(v => v.Id).ToHashSet();
        var batchIds = batchResults.Select(v => v.Id).ToHashSet();
        
        Assert.That(batchIds.SetEquals(originalIds), Is.True,
            "Batch range search should return the same vectors as original search");
    }

    [Test]
    public void IBatchDistanceCalculator_AllCalculatorsImplementInterface()
    {
        // Verify all distance calculators implement batch interface
        var euclidean = new EuclideanDistanceCalculator();
        var cosine = new CosineSimilarityCalculator();
        var manhattan = new ManhattanDistanceCalculator();
        var chebyshev = new ChebyshevDistanceCalculator();
        var minkowski = new MinkowskiDistanceCalculator();

        Assert.That(euclidean, Is.InstanceOf<IBatchDistanceCalculator>());
        Assert.That(cosine, Is.InstanceOf<IBatchDistanceCalculator>());
        Assert.That(manhattan, Is.InstanceOf<IBatchDistanceCalculator>());
        Assert.That(chebyshev, Is.InstanceOf<IBatchDistanceCalculator>());
        Assert.That(minkowski, Is.InstanceOf<IBatchDistanceCalculator>());
    }

    [Test]
    public void BatchCalculation_WithEmptyVectorList_ReturnsEmptyResults()
    {
        // Arrange
        var query = new Vector(GenerateRandomVector(32));
        var emptyList = new List<Vector>();
        var calculator = BatchEuclideanDistanceCalculator.Instance;

        // Act
        var results = calculator.CalculateDistances(query, emptyList);

        // Assert
        Assert.That(results.Length, Is.EqualTo(0));
    }

    [Test]
    public void BatchCalculation_WithNullQuery_ThrowsException()
    {
        // Arrange
        Vector? nullQuery = null;
        var vectors = new List<Vector> { new Vector(GenerateRandomVector(32)) };
        var calculator = BatchEuclideanDistanceCalculator.Instance;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            calculator.CalculateDistances(nullQuery!, vectors));
    }

    [Test]
    public void BatchCalculation_WithDifferentDimensions_ThrowsException()
    {
        // Arrange
        var query = new Vector(GenerateRandomVector(32));
        var vectors = new List<Vector> 
        { 
            new Vector(GenerateRandomVector(64)) // Different dimension
        };
        var calculator = BatchEuclideanDistanceCalculator.Instance;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            calculator.CalculateDistances(query, vectors));
    }

    [Test]
    public void GetOptimalBatchSize_ReturnsReasonableValues()
    {
        // Arrange
        var calculator = BatchEuclideanDistanceCalculator.Instance;

        // Act & Assert
        Assert.That(calculator.GetOptimalBatchSize(128), Is.GreaterThan(0));
        Assert.That(calculator.GetOptimalBatchSize(512), Is.GreaterThan(0));
        Assert.That(calculator.GetOptimalBatchSize(1536), Is.GreaterThan(0));
        
        // Larger dimensions should generally have smaller batch sizes
        Assert.That(calculator.GetOptimalBatchSize(1536), 
            Is.LessThanOrEqualTo(calculator.GetOptimalBatchSize(128)));
    }

    [Test]
    public void ParallelBatchDistance_ProducesCorrectResults()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 1000; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(128)));
        }
        var query = new Vector(GenerateRandomVector(128));

        // Act
        float[] sequentialResults = query.BatchDistance(vectors);
        float[] parallelResults = query.ParallelBatchDistance(vectors);

        // Assert
        Assert.That(parallelResults.Length, Is.EqualTo(sequentialResults.Length));
        
        // Results should be the same (within floating point tolerance)
        for (int i = 0; i < sequentialResults.Length; i++)
        {
            Assert.That(parallelResults[i], Is.EqualTo(sequentialResults[i]).Within(1e-5f));
        }
    }
}