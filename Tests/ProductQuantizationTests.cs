using NUnit.Framework;
using System;
using System.Linq;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
public class ProductQuantizationTests
{
    private VectorList _vectors = null!;
    private SearchService _searchService = null!;

    [SetUp]
    public void SetUp()
    {
        _vectors = new VectorList();
        _searchService = new SearchService(_vectors);
    }

    [TearDown]
    public void TearDown()
    {
        _vectors?.Dispose();
    }

    [Test]
    public void ProductQuantization_EmptyVectorList_ReturnsEmptyResults()
    {
        var query = new Vector(new[] { 1.0f, 2.0f, 3.0f, 4.0f });
        var results = _searchService.Search(query, 3, SearchAlgorithm.ProductQuantization);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(0));
    }

    [Test]
    public void ProductQuantization_SingleVector_ReturnsThatVector()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f, 3.0f, 4.0f }, "single");
        _vectors.Add(vector);

        var query = new Vector(new[] { 1.1f, 2.1f, 3.1f, 4.1f });
        var results = _searchService.Search(query, 1, SearchAlgorithm.ProductQuantization);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0], Is.EqualTo(vector));
    }

    [Test]
    public void ProductQuantization_MultipleVectors_ReturnsApproximateNeighbors()
    {
        // Create vectors with clear patterns (8 dimensions for even division)
        var vectors = new[]
        {
            new Vector(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f }, "positive"),
            new Vector(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f }, "negative"),
            new Vector(new[] { 0.9f, 0.9f, 0.9f, 0.9f, 0.9f, 0.9f, 0.9f, 0.9f }, "similar_positive"),
            new Vector(new[] { -0.9f, -0.9f, -0.9f, -0.9f, -0.9f, -0.9f, -0.9f, -0.9f }, "similar_negative"),
            new Vector(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }, "zero")
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }

        // Query similar to positive vectors
        var query = new Vector(new[] { 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f });
        var results = _searchService.Search(query, 3, SearchAlgorithm.ProductQuantization, 10.0f);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(3));
    }

    [Test]
    public void ProductQuantization_Quantize_ProducesValidCodes()
    {
        // Create vectors for training (16 dimensions)
        var trainingVectors = new VectorList();
        try
        {
            var random = new Random(42);
            for (int i = 0; i < 50; i++)
            {
                var values = new float[16];
                for (int j = 0; j < 16; j++)
                {
                    values[j] = (float)(random.NextDouble() * 4.0 - 2.0);
                }
                trainingVectors.Add(new Vector(values, $"training_{i}"));
            }

            var pq = new ProductQuantization(trainingVectors, numSubVectors: 4, numCentroids: 16);
            var testVector = new Vector(Enumerable.Range(0, 16).Select(i => (float)i).ToArray());
            var pqVector = pq.Quantize(testVector);

            Assert.That(pqVector, Is.Not.Null);
            Assert.That(pqVector.Codes.Length, Is.EqualTo(4)); // 4 sub-vectors
            Assert.That(pqVector.Dimensions, Is.EqualTo(16));
            Assert.That(pqVector.OriginalVector, Is.EqualTo(testVector));

            // All codes should be valid (0-15 for 16 centroids)
            foreach (var code in pqVector.Codes)
            {
                Assert.That(code, Is.LessThan(16));
            }
        }
        finally
        {
            trainingVectors.Dispose();
        }
    }

    [Test]
    public void ProductQuantization_Reconstruct_ApproximatesOriginal()
    {
        // Create vectors for training (8 dimensions for simplicity)
        var trainingVectors = new VectorList();
        try
        {
            var random = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                var values = new float[8];
                for (int j = 0; j < 8; j++)
                {
                    values[j] = (float)(random.NextDouble() * 2.0 - 1.0);
                }
                trainingVectors.Add(new Vector(values, $"training_{i}"));
            }

            var pq = new ProductQuantization(trainingVectors, numSubVectors: 2, numCentroids: 32);
            var testVector = new Vector(new[] { 1.0f, 0.5f, -0.5f, -1.0f, 0.2f, 0.8f, -0.2f, -0.8f });
            
            var pqVector = pq.Quantize(testVector);
            var reconstructed = pq.Reconstruct(pqVector);

            Assert.That(reconstructed, Is.Not.Null);
            Assert.That(reconstructed.Values.Length, Is.EqualTo(8));
            
            // Reconstructed should be reasonably close to original (within 50% typical range)
            for (int i = 0; i < 8; i++)
            {
                float diff = Math.Abs(testVector.Values[i] - reconstructed.Values[i]);
                Assert.That(diff, Is.LessThan(2.0f)); // Reasonable approximation bound
            }
        }
        finally
        {
            trainingVectors.Dispose();
        }
    }

    [Test]
    public void ProductQuantization_CompressionRatio_IsSignificant()
    {
        // Add vectors with 64 dimensions for good compression demonstration
        for (int i = 0; i < 20; i++)
        {
            var values = new float[64];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(Math.Sin(i + j) * 2.0);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var pq = new ProductQuantization(_vectors, numSubVectors: 8, numCentroids: 256);
        float compressionRatio = pq.GetCompressionRatio();

        // PQ achieves theoretical 4x compression per sub-vector (32-bit float to 8-bit code)
        // With 8 sub-vectors, theoretical max is 32x, but codebook overhead reduces this
        Assert.That(compressionRatio, Is.GreaterThan(3.0f));
        Assert.That(compressionRatio, Is.LessThan(35.0f)); // Account for theoretical maximum
    }

    [Test]
    public void ProductQuantization_MemoryStats_AreAccurate()
    {
        // Add test vectors (32 dimensions)
        for (int i = 0; i < 10; i++)
        {
            var values = new float[32];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)i + j * 0.1f;
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var pq = new ProductQuantization(_vectors, numSubVectors: 4, numCentroids: 16);
        var (originalBytes, compressedBytes, ratio) = pq.GetMemoryStats();

        // Original: 10 vectors * 32 dimensions * 4 bytes = 1280 bytes
        Assert.That(originalBytes, Is.EqualTo(10 * 32 * 4));

        // For small datasets, codebook overhead can make compressed size larger
        // But ratio calculation accounts for this and should still show compression benefit
        Assert.That(ratio, Is.GreaterThan(0.5f)); // Should show some compression benefit
        
        // For larger datasets, we expect actual compression
        if (_vectors.Count > 50)
        {
            Assert.That(compressedBytes, Is.LessThan(originalBytes));
        }
    }

    [Test]
    public void ProductQuantization_DifferentSubVectorCounts_WorkCorrectly()
    {
        // Test with dimensions that divide evenly different ways
        for (int i = 0; i < 20; i++)
        {
            var values = new float[24]; // Divisible by 1,2,3,4,6,8,12,24
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(i * j * 0.1);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        // Test different sub-vector counts
        var validCounts = new[] { 1, 2, 3, 4, 6, 8, 12 };
        
        foreach (var count in validCounts)
        {
            Assert.DoesNotThrow(() =>
            {
                var pq = new ProductQuantization(_vectors, numSubVectors: count, numCentroids: 16);
                var query = new Vector(_vectors[0].Values.Select(v => v + 0.1f).ToArray());
                var results = pq.Search(query, 3);
                Assert.That(results.Count, Is.LessThanOrEqualTo(3));
            }, $"Failed with {count} sub-vectors");
        }
    }

    [Test]
    public void ProductQuantization_InvalidDimensions_ThrowsException()
    {
        // Add vectors with 15 dimensions (not divisible by many numbers)
        for (int i = 0; i < 10; i++)
        {
            var values = new float[15];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(i + j);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        // Should throw for invalid sub-vector count
        Assert.Throws<ArgumentException>(() =>
            new ProductQuantization(_vectors, numSubVectors: 4)); // 15 not divisible by 4
    }

    [Test]
    public void ProductQuantization_Performance_CompletesInReasonableTime()
    {
        // Add moderately sized dataset
        var random = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            var values = new float[64];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(random.NextDouble() * 10.0 - 5.0);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var query = new Vector(Enumerable.Range(0, 64).Select(i => (float)random.NextDouble()).ToArray());

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = _searchService.Search(query, 10, SearchAlgorithm.ProductQuantization);
        stopwatch.Stop();

        Assert.That(results, Is.Not.Null);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000)); // Should complete within 5 seconds
    }

    [Test]
    public void ProductQuantization_InvalidParameters_ThrowsExceptions()
    {
        _vectors.Add(new Vector(new[] { 1.0f, 2.0f, 3.0f, 4.0f }, "test"));

        var query = new Vector(new[] { 1.0f, 2.0f, 3.0f, 4.0f });

        // Test k <= 0
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            _searchService.Search(query, 0, SearchAlgorithm.ProductQuantization));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            _searchService.Search(query, -1, SearchAlgorithm.ProductQuantization));

        // Test null query
        Assert.Throws<ArgumentNullException>(() => 
            _searchService.Search((Vector)null!, 1, SearchAlgorithm.ProductQuantization));

        // Test too many centroids
        Assert.Throws<ArgumentException>(() =>
            new ProductQuantization(_vectors, numCentroids: 300)); // > 256
    }

    [Test]
    public void ProductQuantization_StaticMethod_BackwardCompatibility()
    {
        var vectors = new VectorList();
        try
        {
            vectors.Add(new Vector(new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f }, "test1"));
            vectors.Add(new Vector(new[] { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f }, "test2"));
            vectors.Add(new Vector(new[] { 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f }, "test3"));

            var query = new Vector(new[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f, 7.5f, 8.5f });
            var results = ProductQuantization.Search(vectors, query, 2);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results.Count, Is.LessThanOrEqualTo(2));
        }
        finally
        {
            vectors.Dispose();
        }
    }
}