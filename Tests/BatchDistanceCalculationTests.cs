using NUnit.Framework;
using Neighborly;
using Neighborly.Distance;
using Neighborly.Search;
using Neighborly.Tests.Helpers;
using System;
using System.Linq;

namespace Tests;

[TestFixture]
public class BatchDistanceCalculationTests
{
    private readonly Random _random = new(TestConstants.RandomSeed);

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
        for (int i = 0; i < TestConstants.Counts.Default; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(TestConstants.Dimensions.Standard)));
        }
        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Standard));

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
            Assert.That(batchResults[i], Is.EqualTo(singleResults[i]).Within(TestConstants.Tolerances.Default),
                $"Distance mismatch at index {i}");
        }
    }

    [Test]
    public void BatchCosineSimilarity_ProducesCorrectResults()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < TestConstants.Counts.SmallMedium; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(TestConstants.Dimensions.Medium)));
        }
        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Medium));

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
            Assert.That(batchResults[i], Is.EqualTo(singleResults[i]).Within(TestConstants.Tolerances.Default),
                $"Similarity mismatch at index {i}");
        }
    }

    [Test]
    public void BatchDistance_ExtensionMethod_Works()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < TestConstants.Counts.MediumSmall; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(TestConstants.Dimensions.Small)));
        }
        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Small));

        // Act
        float[] batchResults = query.BatchDistance(vectors);

        // Assert
        Assert.That(batchResults.Length, Is.EqualTo(vectors.Count));

        // Verify results match individual calculations
        for (int i = 0; i < vectors.Count; i++)
        {
            float expected = query.Distance(vectors[i]);
            Assert.That(batchResults[i], Is.EqualTo(expected).Within(TestConstants.Tolerances.Default));
        }
    }

    [Test]
    public void BatchOptimizedLinearSearch_ProducesCorrectResults()
    {
        // Arrange
        var vectorList = new VectorList();
        var vectors = new List<Vector>();

        for (int i = 0; i < TestConstants.Counts.Default; i++)
        {
            var vector = new Vector(GenerateRandomVector(TestConstants.Dimensions.Medium));
            vectorList.Add(vector);
            vectors.Add(vector);
        }

        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Medium));
        int k = TestConstants.Search.LargeK;

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

        for (int i = 0; i < TestConstants.Counts.SmallMedium; i++)
        {
            vectorList.Add(new Vector(GenerateRandomVector(TestConstants.Dimensions.Small)));
        }

        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Small));
        float radius = 5.0f; // Range search radius - specific to this test's data distribution

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
        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Small));
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
        var vectors = new List<Vector> { new Vector(GenerateRandomVector(TestConstants.Dimensions.Small)) };
        var calculator = BatchEuclideanDistanceCalculator.Instance;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            calculator.CalculateDistances(nullQuery!, vectors));
    }

    [Test]
    public void BatchCalculation_WithDifferentDimensions_ThrowsException()
    {
        // Arrange
        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Small));
        var vectors = new List<Vector>
        {
            new Vector(GenerateRandomVector(TestConstants.Dimensions.Medium)) // Different dimension
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
        Assert.That(calculator.GetOptimalBatchSize(TestConstants.Dimensions.Standard), Is.GreaterThan(0));
        Assert.That(calculator.GetOptimalBatchSize(TestConstants.Dimensions.XLarge), Is.GreaterThan(0));
        Assert.That(calculator.GetOptimalBatchSize(TestConstants.Dimensions.OpenAIEmbedding), Is.GreaterThan(0));

        // Larger dimensions should generally have smaller batch sizes
        Assert.That(calculator.GetOptimalBatchSize(TestConstants.Dimensions.OpenAIEmbedding),
            Is.LessThanOrEqualTo(calculator.GetOptimalBatchSize(TestConstants.Dimensions.Standard)));
    }

    [Test]
    public void ParallelBatchDistance_ProducesCorrectResults()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < TestConstants.Counts.Large; i++)
        {
            vectors.Add(new Vector(GenerateRandomVector(TestConstants.Dimensions.Standard)));
        }
        var query = new Vector(GenerateRandomVector(TestConstants.Dimensions.Standard));

        // Act
        float[] sequentialResults = query.BatchDistance(vectors);
        float[] parallelResults = query.ParallelBatchDistance(vectors);

        // Assert
        Assert.That(parallelResults.Length, Is.EqualTo(sequentialResults.Length));

        // Results should be the same (within floating point tolerance)
        for (int i = 0; i < sequentialResults.Length; i++)
        {
            Assert.That(parallelResults[i], Is.EqualTo(sequentialResults[i]).Within(TestConstants.Tolerances.Default));
        }
    }
}