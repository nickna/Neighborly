using NUnit.Framework;
using System;
using System.Linq;
using Neighborly;
using Neighborly.Search;
using Neighborly.Distance;

namespace Tests;

[TestFixture]
public class BinaryQuantizationTests
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
    public void BinaryQuantization_EmptyVectorList_ReturnsEmptyResults()
    {
        var query = new Vector(new[] { 1.0f, 2.0f });
        var results = _searchService.Search(query, 3, SearchAlgorithm.BinaryQuantization);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(0));
    }

    [Test]
    public void BinaryQuantization_SingleVector_ReturnsThatVector()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f }, "single");
        _vectors.Add(vector);

        var query = new Vector(new[] { 1.1f, 2.1f });
        var results = _searchService.Search(query, 1, SearchAlgorithm.BinaryQuantization);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0], Is.EqualTo(vector));
    }

    [Test]
    public void BinaryQuantization_MultipleVectors_ReturnsApproximateNeighbors()
    {
        // Create vectors with clear patterns
        var vectors = new[]
        {
            new Vector(new[] { 1.0f, 1.0f, 1.0f, 1.0f }, "positive"),
            new Vector(new[] { -1.0f, -1.0f, -1.0f, -1.0f }, "negative"),
            new Vector(new[] { 0.9f, 0.9f, 0.9f, 0.9f }, "similar_positive"),
            new Vector(new[] { -0.9f, -0.9f, -0.9f, -0.9f }, "similar_negative"),
            new Vector(new[] { 0.0f, 0.0f, 0.0f, 0.0f }, "zero")
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }

        // Query similar to positive vectors
        var query = new Vector(new[] { 0.8f, 0.8f, 0.8f, 0.8f });
        var results = _searchService.Search(query, 3, SearchAlgorithm.BinaryQuantization, 10.0f);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(3));
    }

    [Test]
    public void BinaryQuantization_Quantize_ProducesCorrectBinaryRepresentation()
    {
        var vector = new Vector(new[] { 1.0f, -1.0f, 0.5f, -0.5f });
        float threshold = 0.0f;

        var binaryVector = BinaryQuantization.Quantize(vector, threshold);

        Assert.That(binaryVector, Is.Not.Null);
        Assert.That(binaryVector.Dimensions, Is.EqualTo(4));
        Assert.That(binaryVector.OriginalVector, Is.EqualTo(vector));

        // Check that binary representation is correct
        // Values >= 0: 1.0, 0.5 should be 1
        // Values < 0: -1.0, -0.5 should be 0
        // So binary representation should be: 1, 0, 1, 0 = bits 0 and 2 set
        ulong expectedBits = (1UL << 0) | (1UL << 2); // bits 0 and 2
        Assert.That(binaryVector.BinaryData[0] & 0xF, Is.EqualTo(expectedBits & 0xF));
    }

    [Test]
    public void BinaryQuantization_HammingDistance_CalculatesCorrectly()
    {
        var vector1 = new Vector(new[] { 1.0f, 1.0f, 1.0f, 1.0f }); // All positive
        var vector2 = new Vector(new[] { -1.0f, -1.0f, -1.0f, -1.0f }); // All negative
        var vector3 = new Vector(new[] { 1.0f, -1.0f, 1.0f, -1.0f }); // Alternating

        float threshold = 0.0f;
        var binary1 = BinaryQuantization.Quantize(vector1, threshold);
        var binary2 = BinaryQuantization.Quantize(vector2, threshold);
        var binary3 = BinaryQuantization.Quantize(vector3, threshold);

        // Distance between all positive and all negative should be 4 (all bits different)
        int dist12 = binary1.HammingDistance(binary2);
        Assert.That(dist12, Is.EqualTo(4));

        // Distance between all positive and alternating should be 2 (half bits different)
        int dist13 = binary1.HammingDistance(binary3);
        Assert.That(dist13, Is.EqualTo(2));

        // Distance between all negative and alternating should be 2
        int dist23 = binary2.HammingDistance(binary3);
        Assert.That(dist23, Is.EqualTo(2));
    }

    [Test]
    public void BinaryQuantization_CompressionRatio_IsSignificant()
    {
        // Add vectors with different dimensionalities
        for (int i = 0; i < 10; i++)
        {
            var values = new float[128]; // 128 dimensions
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(Math.Sin(i + j) * 2.0);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var bq = new BinaryQuantization(_vectors);
        float compressionRatio = bq.GetCompressionRatio();

        // Should achieve significant compression (close to 32x for float32 -> binary)
        Assert.That(compressionRatio, Is.GreaterThan(20.0f));
        Assert.That(compressionRatio, Is.LessThan(40.0f));
    }

    [Test]
    public void BinaryQuantization_MemoryStats_AreAccurate()
    {
        // Add test vectors
        for (int i = 0; i < 5; i++)
        {
            var values = new float[64];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)i;
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var bq = new BinaryQuantization(_vectors);
        var (originalBytes, compressedBytes, ratio) = bq.GetMemoryStats();

        // Original: 5 vectors * 64 dimensions * 4 bytes = 1280 bytes
        Assert.That(originalBytes, Is.EqualTo(5 * 64 * 4));

        // Compressed should be much smaller
        Assert.That(compressedBytes, Is.LessThan(originalBytes));
        Assert.That(ratio, Is.GreaterThan(1.0f));
    }

    [Test]
    public void BinaryQuantization_WithCustomThreshold_WorksCorrectly()
    {
        var vectors = new[]
        {
            new Vector(new[] { 2.0f, 4.0f, 6.0f }, "high"),
            new Vector(new[] { 1.0f, 2.0f, 3.0f }, "medium"),
            new Vector(new[] { 0.5f, 1.0f, 1.5f }, "low")
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }

        // Use custom threshold
        float customThreshold = 2.0f;
        var bq = new BinaryQuantization(_vectors, customThreshold);

        var query = new Vector(new[] { 1.8f, 3.8f, 5.8f });
        var results = bq.Search(query, 2);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.LessThanOrEqualTo(2));
    }

    [Test]
    public void BinaryQuantization_HighDimensionalVectors_HandlesEfficiently()
    {
        // Create high-dimensional vectors
        var random = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var values = new float[256];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(random.NextDouble() * 4.0 - 2.0);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var query = new Vector(_vectors[0].Values.Select(v => v + 0.1f).ToArray());
        var results = _searchService.Search(query, 5, SearchAlgorithm.BinaryQuantization);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.LessThanOrEqualTo(5));
    }

    [Test]
    public void BinaryQuantization_Performance_CompletesInReasonableTime()
    {
        // Add many vectors to test performance
        var random = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            var values = new float[128];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)(random.NextDouble() * 10.0 - 5.0);
            }
            _vectors.Add(new Vector(values, $"vector_{i}"));
        }

        var query = new Vector(Enumerable.Range(0, 128).Select(i => (float)random.NextDouble()).ToArray());

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = _searchService.Search(query, 10, SearchAlgorithm.BinaryQuantization);
        stopwatch.Stop();

        Assert.That(results, Is.Not.Null);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000)); // Should be very fast
    }

    [Test]
    public void BinaryQuantization_InvalidParameters_ThrowsExceptions()
    {
        _vectors.Add(new Vector(new[] { 1.0f, 2.0f }, "test"));

        var query = new Vector(new[] { 1.0f, 2.0f });

        // Test k <= 0
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            _searchService.Search(query, 0, SearchAlgorithm.BinaryQuantization));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            _searchService.Search(query, -1, SearchAlgorithm.BinaryQuantization));

        // Test null query
        Assert.Throws<ArgumentNullException>(() => 
            _searchService.Search((Vector)null!, 1, SearchAlgorithm.BinaryQuantization));
    }

    [Test]
    public void BinaryQuantization_StaticMethod_BackwardCompatibility()
    {
        var vectors = new VectorList();
        try
        {
            vectors.Add(new Vector(new[] { 1.0f, 2.0f, 3.0f }, "test1"));
            vectors.Add(new Vector(new[] { 2.0f, 3.0f, 4.0f }, "test2"));
            vectors.Add(new Vector(new[] { 3.0f, 4.0f, 5.0f }, "test3"));

            var query = new Vector(new[] { 1.5f, 2.5f, 3.5f });
            var results = BinaryQuantization.Search(vectors, query, 2);

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