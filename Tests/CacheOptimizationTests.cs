using NUnit.Framework;
using Neighborly;
using Neighborly.Distance;
using System;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace Tests;

[TestFixture]
public class CacheOptimizationTests
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
    public void CacheOptimizedVector_CreatesAlignedMemory()
    {
        // Arrange
        float[] values = GenerateRandomVector(128);

        // Act
        using var optimized = new CacheOptimizedVector(values);

        // Assert
        Assert.That(optimized.Dimension, Is.EqualTo(128));
        Assert.That(optimized.GetValues().Length, Is.EqualTo(128));
        
        // Verify values match
        var resultValues = optimized.ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            Assert.That(resultValues[i], Is.EqualTo(values[i]).Within(1e-6f));
        }
    }

    [Test]
    public void CacheOptimizedVector_FromVector_PreservesData()
    {
        // Arrange
        float[] values = GenerateRandomVector(256);
        var originalVector = new Vector(Guid.NewGuid(), values, new short[] { 1, 2, 3 }, "Test Vector");

        // Act
        using var optimized = CacheOptimizedVector.FromVector(originalVector);

        // Assert
        Assert.That(optimized.Id, Is.EqualTo(originalVector.Id));
        Assert.That(optimized.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(optimized.Tags, Is.EqualTo(originalVector.Tags));
        Assert.That(optimized.Dimension, Is.EqualTo(originalVector.Dimension));
    }

    [Test]
    public void CacheOptimizedVector_ToVector_RoundTrip()
    {
        // Arrange
        float[] values = GenerateRandomVector(512);
        var originalVector = new Vector(values, "Round Trip Test");

        // Act
        using var optimized = CacheOptimizedVector.FromVector(originalVector);
        var resultVector = optimized.ToVector();

        // Assert
        Assert.That(resultVector.Id, Is.EqualTo(originalVector.Id));
        Assert.That(resultVector.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(resultVector.Dimension, Is.EqualTo(originalVector.Dimension));
        
        // Verify values
        for (int i = 0; i < originalVector.Dimension; i++)
        {
            Assert.That(resultVector.Values[i], Is.EqualTo(originalVector.Values[i]).Within(1e-6f));
        }
    }

    [Test]
    public void CacheOptimizedEuclideanDistance_ProducesCorrectResults()
    {
        // Arrange
        float[] values1 = { 1, 2, 3, 4, 5 };
        float[] values2 = { 2, 4, 6, 8, 10 };
        
        var vector1 = new Vector(values1);
        var vector2 = new Vector(values2);
        
        // Calculate expected distance manually
        float expectedDistance = MathF.Sqrt(1 + 4 + 9 + 16 + 25); // sqrt(55) ≈ 7.416

        // Act
        var regularCalc = new EuclideanDistanceCalculator();
        var regularDistance = regularCalc.CalculateDistance(vector1, vector2);
        
        using var opt1 = CacheOptimizedVector.FromVector(vector1);
        using var opt2 = CacheOptimizedVector.FromVector(vector2);
        var optimizedCalc = CacheOptimizedEuclideanDistance.Instance;
        var optimizedDistance = optimizedCalc.CalculateDistance(opt1, opt2);

        // Assert
        Assert.That(regularDistance, Is.EqualTo(expectedDistance).Within(1e-5f));
        Assert.That(optimizedDistance, Is.EqualTo(expectedDistance).Within(1e-5f));
        Assert.That(optimizedDistance, Is.EqualTo(regularDistance).Within(1e-6f));
    }

    [Test]
    public void CacheOptimizedCosineSimilarity_ProducesCorrectResults()
    {
        // Arrange
        float[] values1 = { 1, 0, 1, 0 };
        float[] values2 = { 0, 1, 0, 1 };
        
        var vector1 = new Vector(values1);
        var vector2 = new Vector(values2);

        // Act
        var regularCalc = new CosineSimilarityCalculator();
        var regularSimilarity = regularCalc.CalculateDistance(vector1, vector2);
        
        using var opt1 = CacheOptimizedVector.FromVector(vector1);
        using var opt2 = CacheOptimizedVector.FromVector(vector2);
        var optimizedCalc = CacheOptimizedCosineSimilarity.Instance;
        var optimizedSimilarity = optimizedCalc.CalculateDistance(opt1, opt2);

        // Assert
        Assert.That(optimizedSimilarity, Is.EqualTo(regularSimilarity).Within(1e-6f));
        Assert.That(optimizedSimilarity, Is.EqualTo(0f).Within(1e-6f)); // Orthogonal vectors
    }

    [Test]
    public void CacheOptimizedVectorBatch_HandlesMultipleVectors()
    {
        // Arrange
        var vectors = new List<Vector>();
        for (int i = 0; i < 100; i++)
        {
            float[] values = GenerateRandomVector(64);
            vectors.Add(new Vector(values, $"Vector_{i}"));
        }

        // Act
        using var batch = new CacheOptimizedVectorBatch(vectors);

        // Assert
        Assert.That(batch.Count, Is.EqualTo(100));
        Assert.That(batch.Dimension, Is.EqualTo(64));

        // Verify individual vectors
        for (int i = 0; i < vectors.Count; i++)
        {
            var span = batch.GetVectorSpan(i);
            Assert.That(span.Length, Is.EqualTo(64));
            
            // Verify values match
            for (int j = 0; j < vectors[i].Dimension; j++)
            {
                Assert.That(span[j], Is.EqualTo(vectors[i].Values[j]).Within(1e-6f));
            }
        }
    }

    [Test]
    public void CacheOptimizedVectorPool_RentsAndReturns()
    {
        // Arrange
        var pool = new CacheOptimizedVectorPool(128, maxPoolSize: 10);
        float[] values1 = GenerateRandomVector(128);
        float[] values2 = GenerateRandomVector(128);

        // Act & Assert
        Assert.That(pool.CurrentPoolSize, Is.EqualTo(0));

        // Rent and return first vector
        PooledVector pooled1;
        using (pooled1 = pool.Rent(values1))
        {
            Assert.That(pool.CurrentPoolSize, Is.EqualTo(0)); // Not in pool while rented
            Assert.That(pooled1.Vector.Dimension, Is.EqualTo(128));
        }
        
        Assert.That(pool.CurrentPoolSize, Is.EqualTo(1)); // Returned to pool

        // Rent again - should reuse pooled vector
        using (var pooled2 = pool.Rent(values2))
        {
            Assert.That(pool.CurrentPoolSize, Is.EqualTo(0));
            Assert.That(pooled2.Vector.Dimension, Is.EqualTo(128));
        }

        Assert.That(pool.CurrentPoolSize, Is.EqualTo(1));
    }

    [Test]
    public void CacheOptimizedVectorPool_SharedPoolSingleton()
    {
        // Act
        var pool1 = CacheOptimizedVectorPool.GetSharedPool(256);
        var pool2 = CacheOptimizedVectorPool.GetSharedPool(256);
        var pool3 = CacheOptimizedVectorPool.GetSharedPool(512);

        // Assert
        Assert.That(pool1, Is.SameAs(pool2)); // Same dimension returns same pool
        Assert.That(pool1, Is.Not.SameAs(pool3)); // Different dimension returns different pool
        Assert.That(pool1.Dimension, Is.EqualTo(256));
        Assert.That(pool3.Dimension, Is.EqualTo(512));
    }

    [Test]
    public void ExtensionMethods_CacheOptimizedDistance_Works()
    {
        // Arrange
        float[] values1 = GenerateRandomVector(128);
        float[] values2 = GenerateRandomVector(128);
        var vector1 = new Vector(values1);
        var vector2 = new Vector(values2);

        // Act
        var regularDistance = vector1.Distance(vector2);
        var optimizedDistance = vector1.CacheOptimizedDistance(vector2);

        // Assert
        Assert.That(optimizedDistance, Is.EqualTo(regularDistance).Within(1e-5f));
    }

    [Test]
    public void ExtensionMethods_CacheOptimizedBatchDistance_Works()
    {
        // Arrange
        float[] queryValues = GenerateRandomVector(64);
        var queryVector = new Vector(queryValues);
        
        var targetVectors = new List<Vector>();
        for (int i = 0; i < 10; i++)
        {
            targetVectors.Add(new Vector(GenerateRandomVector(64)));
        }

        // Act
        var distances = queryVector.CacheOptimizedBatchDistance(targetVectors);

        // Assert
        Assert.That(distances.Length, Is.EqualTo(10));
        
        // Verify distances are correct
        for (int i = 0; i < targetVectors.Count; i++)
        {
            var expectedDistance = queryVector.Distance(targetVectors[i]);
            Assert.That(distances[i], Is.EqualTo(expectedDistance).Within(1e-5f));
        }
    }

    [Test]
    public void SimdSupport_ReportsCorrectly()
    {
        // This test just verifies SIMD detection works
        Console.WriteLine($"AVX Support: {Avx.IsSupported}");
        Console.WriteLine($"SSE Support: {Sse.IsSupported}");
        
        // Test should pass regardless of SIMD support
        Assert.Pass($"SIMD Support - AVX: {Avx.IsSupported}, SSE: {Sse.IsSupported}");
    }
}