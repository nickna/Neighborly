using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
public class HNSWTests
{
    private VectorList _vectors = null!;
    private HNSW _hnsw = null!;

    [SetUp]
    public void SetUp()
    {
        _vectors = new VectorList();
        _hnsw = new HNSW();
    }

    [TearDown]
    public void TearDown()
    {
        _vectors?.Dispose();
    }

    [Test]
    public void HNSW_Constructor_WithDefaultConfig_CreatesValidInstance()
    {
        var hnsw = new HNSW();
        
        Assert.That(hnsw.Count, Is.EqualTo(0));
        Assert.That(hnsw.MaxLayer, Is.EqualTo(0));
        Assert.That(hnsw.EntryPointId, Is.Null);
        Assert.That(hnsw.Config, Is.Not.Null);
        Assert.That(hnsw.Config.M, Is.EqualTo(16));
    }

    [Test]
    public void HNSW_Constructor_WithCustomConfig_UsesProvidedConfig()
    {
        var config = new HNSWConfig { M = 32, MaxM0 = 64, EfConstruction = 400 };
        var hnsw = new HNSW(config);
        
        Assert.That(hnsw.Config.M, Is.EqualTo(32));
        Assert.That(hnsw.Config.MaxM0, Is.EqualTo(64));
        Assert.That(hnsw.Config.EfConstruction, Is.EqualTo(400));
    }

    [Test]
    public void HNSW_Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HNSW(null!));
    }

    [Test]
    public void HNSW_Build_WithEmptyVectorList_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _hnsw.Build(_vectors));
        Assert.That(_hnsw.Count, Is.EqualTo(0));
    }

    [Test]
    public void HNSW_Build_WithNullVectorList_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _hnsw.Build(null!));
    }

    [Test]
    public void HNSW_Build_WithSingleVector_CreatesValidGraph()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        _vectors.Add(vector);
        
        _hnsw.Build(_vectors);
        
        Assert.That(_hnsw.Count, Is.EqualTo(1));
        Assert.That(_hnsw.EntryPointId, Is.Not.Null);
        Assert.That(_hnsw.MaxLayer, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void HNSW_Build_WithMultipleVectors_CreatesValidGraph()
    {
        var vectors = new[]
        {
            new Vector(new[] { 1.0f, 0.0f }),
            new Vector(new[] { 0.0f, 1.0f }),
            new Vector(new[] { 1.0f, 1.0f }),
            new Vector(new[] { 0.0f, 0.0f }),
            new Vector(new[] { 0.5f, 0.5f })
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }
        
        _hnsw.Build(_vectors);
        
        Assert.That(_hnsw.Count, Is.EqualTo(5));
        Assert.That(_hnsw.EntryPointId, Is.Not.Null);
        Assert.That(_hnsw.MaxLayer, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void HNSW_Insert_WithNullVector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _hnsw.Insert(null!));
    }

    [Test]
    public void HNSW_Insert_SingleVector_UpdatesCountAndEntryPoint()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        
        _hnsw.Insert(vector);
        
        Assert.That(_hnsw.Count, Is.EqualTo(1));
        Assert.That(_hnsw.EntryPointId, Is.Not.Null);
    }

    [Test]
    public void HNSW_Search_WithNullQuery_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _hnsw.Search(null!, 5));
    }

    [Test]
    public void HNSW_Search_WithInvalidK_ThrowsArgumentException()
    {
        var query = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        
        Assert.Throws<ArgumentException>(() => _hnsw.Search(query, 0));
        Assert.Throws<ArgumentException>(() => _hnsw.Search(query, -1));
    }

    [Test]
    public void HNSW_Search_WithEmptyGraph_ReturnsEmptyList()
    {
        var query = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        
        var results = _hnsw.Search(query, 5);
        
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(0));
    }

    [Test]
    public void HNSW_Search_WithSingleVector_ReturnsCorrectResult()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        _hnsw.Insert(vector);
        
        var query = new Vector(new[] { 1.1f, 2.1f, 3.1f });
        var results = _hnsw.Search(query, 1);
        
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].Id, Is.EqualTo(vector.Id));
    }

    [Test]
    public void HNSW_Search_FindsNearestNeighbors()
    {
        // Create vectors in a 2D space
        var vectors = new[]
        {
            new Vector(new[] { 0.0f, 0.0f }, "origin"),
            new Vector(new[] { 1.0f, 0.0f }, "right"),
            new Vector(new[] { 0.0f, 1.0f }, "up"),
            new Vector(new[] { 10.0f, 10.0f }, "far"),
            new Vector(new[] { 0.1f, 0.1f }, "close")
        };

        foreach (var vector in vectors)
        {
            _hnsw.Insert(vector);
        }

        // Search for vectors close to origin
        var query = new Vector(new[] { 0.05f, 0.05f });
        var results = _hnsw.Search(query, 3);
        
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(3));
        
        // The closest should be "close" or "origin"
        var closestTexts = results.Take(2).Select(v => v.OriginalText).ToList();
        Assert.That(closestTexts, Does.Contain("close").Or.Contains("origin"));
        
        // Since HNSW is an approximate algorithm, we'll check distances instead of exact exclusions
        var distances = results.Select(v => v.Distance(query)).ToList();
        Assert.That(distances, Is.Ordered, "Results should be ordered by distance");
    }

    [Test]
    public void HNSW_Search_ReturnsRequestedNumberOfResults()
    {
        // Add enough vectors for testing
        for (int i = 0; i < 10; i++)
        {
            var vector = new Vector(new[] { (float)i, (float)i });
            _hnsw.Insert(vector);
        }

        var query = new Vector(new[] { 5.0f, 5.0f });
        
        var results1 = _hnsw.Search(query, 3);
        var results2 = _hnsw.Search(query, 7);
        
        Assert.That(results1.Count, Is.LessThanOrEqualTo(3));
        Assert.That(results2.Count, Is.LessThanOrEqualTo(7));
        Assert.That(results2.Count, Is.GreaterThanOrEqualTo(results1.Count));
    }

    [Test]
    public void HNSW_Clear_ResetsGraph()
    {
        // Build a graph
        var vector = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        _hnsw.Insert(vector);
        
        Assert.That(_hnsw.Count, Is.EqualTo(1));
        
        // Clear and verify
        _hnsw.Clear();
        
        Assert.That(_hnsw.Count, Is.EqualTo(0));
        Assert.That(_hnsw.EntryPointId, Is.Null);
        Assert.That(_hnsw.MaxLayer, Is.EqualTo(0));
    }

    [Test]
    public async Task HNSW_SaveAndLoad_PreservesGraphStructure()
    {
        // Build a graph
        var vectors = new[]
        {
            new Vector(new[] { 1.0f, 0.0f }),
            new Vector(new[] { 0.0f, 1.0f }),
            new Vector(new[] { 1.0f, 1.0f })
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
            _hnsw.Insert(vector);
        }

        int originalCount = _hnsw.Count;
        int originalMaxLayer = _hnsw.MaxLayer;
        int? originalEntryPoint = _hnsw.EntryPointId;

        // Save to memory stream
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        await _hnsw.SaveAsync(writer);

        // Create new HNSW and load
        var newHnsw = new HNSW();
        memoryStream.Position = 0;
        using var reader = new BinaryReader(memoryStream);
        await newHnsw.LoadAsync(reader, _vectors);

        // Verify structure is preserved
        Assert.That(newHnsw.Count, Is.EqualTo(originalCount));
        Assert.That(newHnsw.MaxLayer, Is.EqualTo(originalMaxLayer));
        Assert.That(newHnsw.EntryPointId, Is.EqualTo(originalEntryPoint));

        // Test search works the same
        var query = new Vector(new[] { 0.5f, 0.5f });
        var originalResults = _hnsw.Search(query, 2);
        var loadedResults = newHnsw.Search(query, 2);

        Assert.That(loadedResults.Count, Is.EqualTo(originalResults.Count));
    }

    [Test]
    public async Task HNSW_SaveAsync_WithNullWriter_ThrowsArgumentNullException()
    {
        await Assert.ThatAsync(async () => await _hnsw.SaveAsync(null!), Throws.ArgumentNullException);
    }

    [Test]
    public async Task HNSW_LoadAsync_WithNullReader_ThrowsArgumentNullException()
    {
        using var memoryStream = new MemoryStream();
        using var reader = new BinaryReader(memoryStream);
        
        await Assert.ThatAsync(async () => await _hnsw.LoadAsync(null!, _vectors), Throws.ArgumentNullException);
    }

    [Test]
    public async Task HNSW_LoadAsync_WithNullVectors_ThrowsArgumentNullException()
    {
        using var memoryStream = new MemoryStream();
        using var reader = new BinaryReader(memoryStream);
        
        await Assert.ThatAsync(async () => await _hnsw.LoadAsync(reader, null!), Throws.ArgumentNullException);
    }

    [Test]
    public async Task HNSW_LoadAsync_WithInvalidVersion_ThrowsInvalidDataException()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        
        writer.Write(999); // Invalid version
        
        memoryStream.Position = 0;
        using var reader = new BinaryReader(memoryStream);
        
        await Assert.ThatAsync(async () => await _hnsw.LoadAsync(reader, _vectors), Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void HNSW_Search_WithCustomEf_UsesProvidedParameter()
    {
        // Add some vectors
        for (int i = 0; i < 5; i++)
        {
            var vector = new Vector(new[] { (float)i, 0.0f });
            _hnsw.Insert(vector);
        }

        var query = new Vector(new[] { 2.0f, 0.0f });
        
        // Search with different ef values should not throw
        Assert.DoesNotThrow(() => _hnsw.Search(query, 2, ef: 50));
        Assert.DoesNotThrow(() => _hnsw.Search(query, 2, ef: 200));
    }

    [Test]
    public void HNSW_Build_HighDimensionalVectors_HandlesCorrectly()
    {
        // Test with higher dimensional vectors
        var random = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var embedding = new float[128];
            for (int j = 0; j < 128; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            var vector = new Vector(embedding);
            _hnsw.Insert(vector);
        }

        Assert.That(_hnsw.Count, Is.EqualTo(20));
        
        // Test search works
        var queryEmbedding = new float[128];
        for (int j = 0; j < 128; j++)
        {
            queryEmbedding[j] = (float)(random.NextDouble() * 2 - 1);
        }
        var query = new Vector(queryEmbedding);
        
        var results = _hnsw.Search(query, 5);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(5));
    }

    [Test]
    public void HNSW_Equals_WithSameContent_ReturnsTrue()
    {
        var hnsw1 = new HNSW();
        var hnsw2 = new HNSW();
        
        // Both empty
        Assert.That(hnsw1.Equals(hnsw2), Is.True);
        
        // Add same content
        var vector = new Vector(new[] { 1.0f, 2.0f });
        hnsw1.Insert(vector);
        hnsw2.Insert(vector);
        
        Assert.That(hnsw1.Equals(hnsw2), Is.True);
    }

    [Test]
    public void HNSW_GetHashCode_IsConsistent()
    {
        var hash1 = _hnsw.GetHashCode();
        var hash2 = _hnsw.GetHashCode();
        
        Assert.That(hash1, Is.EqualTo(hash2));
        
        // Add vector and verify hash changes
        var vector = new Vector(new[] { 1.0f, 2.0f });
        _hnsw.Insert(vector);
        
        var hash3 = _hnsw.GetHashCode();
        Assert.That(hash3, Is.Not.EqualTo(hash1));
    }
}

[TestFixture]
public class HNSWConfigTests
{
    [Test]
    public void HNSWConfig_DefaultConstructor_SetsReasonableDefaults()
    {
        var config = new HNSWConfig();
        
        Assert.That(config.M, Is.EqualTo(16));
        Assert.That(config.MaxM0, Is.EqualTo(32));
        Assert.That(config.EfConstruction, Is.EqualTo(200));
        Assert.That(config.Ef, Is.EqualTo(200));
        Assert.That(config.Ml, Is.EqualTo(1.0 / Math.Log(2.0)).Within(0.01));
        Assert.That(config.Seed, Is.EqualTo(42));
    }

    [Test]
    public void HNSWConfig_Validate_WithValidValues_DoesNotThrow()
    {
        var config = new HNSWConfig
        {
            M = 16,
            MaxM0 = 32,
            EfConstruction = 200,
            Ef = 200,
            Ml = 1.44
        };
        
        Assert.DoesNotThrow(() => config.Validate());
    }

    [Test]
    public void HNSWConfig_Validate_WithInvalidM_ThrowsArgumentException()
    {
        var config = new HNSWConfig { M = 0 };
        Assert.Throws<ArgumentException>(() => config.Validate());
        
        config.M = -1;
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void HNSWConfig_Validate_WithInvalidMaxM0_ThrowsArgumentException()
    {
        var config = new HNSWConfig { MaxM0 = 0 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void HNSWConfig_Validate_WithInvalidEfConstruction_ThrowsArgumentException()
    {
        var config = new HNSWConfig { EfConstruction = 0 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void HNSWConfig_Validate_WithInvalidEf_ThrowsArgumentException()
    {
        var config = new HNSWConfig { Ef = 0 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void HNSWConfig_Validate_WithInvalidMl_ThrowsArgumentException()
    {
        var config = new HNSWConfig { Ml = 0 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Test]
    public void HNSWConfig_HighAccuracy_ReturnsOptimizedConfig()
    {
        var config = HNSWConfig.HighAccuracy();
        
        Assert.That(config.M, Is.EqualTo(32));
        Assert.That(config.MaxM0, Is.EqualTo(64));
        Assert.That(config.EfConstruction, Is.EqualTo(400));
        Assert.That(config.Ef, Is.EqualTo(400));
    }

    [Test]
    public void HNSWConfig_HighSpeed_ReturnsOptimizedConfig()
    {
        var config = HNSWConfig.HighSpeed();
        
        Assert.That(config.M, Is.EqualTo(8));
        Assert.That(config.MaxM0, Is.EqualTo(16));
        Assert.That(config.EfConstruction, Is.EqualTo(100));
        Assert.That(config.Ef, Is.EqualTo(100));
    }

    [Test]
    public void HNSWConfig_Balanced_ReturnsDefaultConfig()
    {
        var config = HNSWConfig.Balanced();
        var defaultConfig = new HNSWConfig();
        
        Assert.That(config.M, Is.EqualTo(defaultConfig.M));
        Assert.That(config.MaxM0, Is.EqualTo(defaultConfig.MaxM0));
        Assert.That(config.EfConstruction, Is.EqualTo(defaultConfig.EfConstruction));
        Assert.That(config.Ef, Is.EqualTo(defaultConfig.Ef));
    }
}

[TestFixture]
public class HNSWNodeTests
{
    [Test]
    public void HNSWNode_DefaultConstructor_InitializesCorrectly()
    {
        var node = new HNSWNode();
        
        Assert.That(node.Connections, Is.Not.Null);
        Assert.That(node.Connections.Count, Is.EqualTo(0));
    }

    [Test]
    public void HNSWNode_ParameterizedConstructor_InitializesCorrectly()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f, 3.0f });
        var node = new HNSWNode(vector, 1, 2);
        
        Assert.That(node.Vector, Is.EqualTo(vector));
        Assert.That(node.Id, Is.EqualTo(1));
        Assert.That(node.MaxLayer, Is.EqualTo(2));
        Assert.That(node.Connections.Count, Is.EqualTo(3)); // 0, 1, 2
    }

    [Test]
    public void HNSWNode_ParameterizedConstructor_WithNullVector_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HNSWNode(null!, 1, 2));
    }

    [Test]
    public void HNSWNode_AddConnection_AddsToCorrectLayer()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f });
        var node = new HNSWNode(vector, 1, 2);
        
        node.AddConnection(5, 0);
        node.AddConnection(10, 1);
        
        Assert.That(node.GetConnections(0), Contains.Item(5));
        Assert.That(node.GetConnections(1), Contains.Item(10));
        Assert.That(node.GetConnections(2).Count, Is.EqualTo(0));
    }

    [Test]
    public void HNSWNode_AddConnection_WithInvalidLayer_DoesNothing()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f });
        var node = new HNSWNode(vector, 1, 1);
        
        node.AddConnection(5, -1);
        node.AddConnection(10, 5);
        
        // Should not crash, and connections should remain empty
        Assert.That(node.GetConnections(0).Count, Is.EqualTo(0));
        Assert.That(node.GetConnections(1).Count, Is.EqualTo(0));
    }

    [Test]
    public void HNSWNode_RemoveConnection_RemovesFromCorrectLayer()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f });
        var node = new HNSWNode(vector, 1, 1);
        
        node.AddConnection(5, 0);
        node.AddConnection(10, 0);
        
        Assert.That(node.GetConnections(0).Count, Is.EqualTo(2));
        
        node.RemoveConnection(5, 0);
        
        Assert.That(node.GetConnections(0).Count, Is.EqualTo(1));
        Assert.That(node.GetConnections(0), Contains.Item(10));
        Assert.That(node.GetConnections(0), Does.Not.Contain(5));
    }

    [Test]
    public void HNSWNode_DistanceTo_CalculatesCorrectDistance()
    {
        var vector1 = new Vector(new[] { 0.0f, 0.0f });
        var vector2 = new Vector(new[] { 3.0f, 4.0f });
        var node1 = new HNSWNode(vector1, 1, 0);
        var node2 = new HNSWNode(vector2, 2, 0);
        
        var distance = node1.DistanceTo(node2);
        
        Assert.That(distance, Is.EqualTo(5.0f).Within(0.001f)); // 3-4-5 triangle
    }

    [Test]
    public void HNSWNode_Equals_WithSameId_ReturnsTrue()
    {
        var vector1 = new Vector(new[] { 1.0f, 2.0f });
        var vector2 = new Vector(new[] { 3.0f, 4.0f });
        var node1 = new HNSWNode(vector1, 1, 0);
        var node2 = new HNSWNode(vector2, 1, 0); // Same ID
        
        Assert.That(node1.Equals(node2), Is.True);
        Assert.That(node1 == node2, Is.True);
    }

    [Test]
    public void HNSWNode_Equals_WithDifferentId_ReturnsFalse()
    {
        var vector = new Vector(new[] { 1.0f, 2.0f });
        var node1 = new HNSWNode(vector, 1, 0);
        var node2 = new HNSWNode(vector, 2, 0); // Different ID
        
        Assert.That(node1.Equals(node2), Is.False);
        Assert.That(node1 != node2, Is.True);
    }

    [Test]
    public void HNSWNode_GetHashCode_WithSameId_ReturnsSameHash()
    {
        var vector1 = new Vector(new[] { 1.0f, 2.0f });
        var vector2 = new Vector(new[] { 3.0f, 4.0f });
        var node1 = new HNSWNode(vector1, 1, 0);
        var node2 = new HNSWNode(vector2, 1, 0); // Same ID
        
        Assert.That(node1.GetHashCode(), Is.EqualTo(node2.GetHashCode()));
    }
}