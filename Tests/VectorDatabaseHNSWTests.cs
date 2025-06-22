using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
public class VectorDatabaseHNSWTests
{
    private VectorDatabase _database = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new VectorDatabase();
    }

    [TearDown]
    public void TearDown()
    {
        _database?.Dispose();
    }

    [Test]
    public async Task VectorDatabase_Search_WithHNSW_ReturnsResults()
    {
        // Add test data using Vector constructor
        _database.Vectors.Add(new Vector("The quick brown fox jumps over the lazy dog"));
        _database.Vectors.Add(new Vector("Machine learning is a subset of artificial intelligence"));
        _database.Vectors.Add(new Vector("Vector databases enable semantic search capabilities"));
        _database.Vectors.Add(new Vector("HNSW provides efficient approximate nearest neighbor search"));

        // Build HNSW index before searching
        await _database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);

        // Search using HNSW algorithm with more lenient threshold
        var results = _database.Search("machine learning algorithms", 2, SearchAlgorithm.HNSW, 2.0f);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(2));
        
        // Results should be relevant to machine learning
        var texts = results.Select(v => v.OriginalText).ToList();
        Assert.That(texts.Any(t => t.Contains("Machine learning") || t.Contains("HNSW")), Is.True,
            "Results should contain semantically similar content");
    }

    [Test]
    public void VectorDatabase_BuildIndex_HNSW_CompletesSuccessfully()
    {
        // Add some data
        for (int i = 0; i < 100; i++)
        {
            _database.Vectors.Add(new Vector($"Document {i} with some test content for indexing"));
        }

        // Build HNSW index
        Assert.DoesNotThrowAsync(async () => await _database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW));
        
        // Search should work after index is built
        var results = _database.Search("test content", 5, SearchAlgorithm.HNSW, 2.0f);
        Assert.That(results.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task VectorDatabase_Search_HNSW_PerformanceComparison()
    {
        // Add test data
        for (int i = 0; i < 500; i++)
        {
            _database.Vectors.Add(new Vector($"Test document {i} containing various keywords and semantic content for search evaluation"));
        }

        // Build indexes
        await _database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
        await _database.RebuildSearchIndexAsync(SearchAlgorithm.KDTree);

        var query = "semantic search evaluation";
        
        // Test both algorithms return results
        var hnswResults = _database.Search(query, 10, SearchAlgorithm.HNSW, 2.0f);
        var kdtreeResults = _database.Search(query, 10, SearchAlgorithm.KDTree, 2.0f);
        
        Assert.That(hnswResults.Count, Is.GreaterThan(0), "HNSW should return results");
        Assert.That(kdtreeResults.Count, Is.GreaterThan(0), "KDTree should return results");
        
        // Both should return some similar results (approximate algorithms)
        var hnswIds = hnswResults.Select(v => v.Id).ToHashSet();
        var kdtreeIds = kdtreeResults.Select(v => v.Id).ToHashSet();
        var intersection = hnswIds.Intersect(kdtreeIds).Count();
        
        // At least some overlap expected, but exact match not required due to different algorithms
        Assert.That(intersection, Is.GreaterThan(0), "HNSW and KDTree should return some overlapping results");
    }

    [Test]
    public async Task VectorDatabase_HNSW_HandlesSimilarityThreshold()
    {
        // Add vectors with known relationships
        _database.Vectors.Add(new Vector("artificial intelligence and machine learning"));
        _database.Vectors.Add(new Vector("deep learning neural networks"));
        _database.Vectors.Add(new Vector("completely different topic about cooking recipes"));
        
        await _database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
        
        // Search with strict similarity threshold
        var strictResults = _database.Search("machine learning", 10, SearchAlgorithm.HNSW, 0.3f);
        var lenientResults = _database.Search("machine learning", 10, SearchAlgorithm.HNSW, 0.8f);
        
        Assert.That(strictResults.Count, Is.LessThanOrEqualTo(lenientResults.Count),
            "Stricter threshold should return fewer or equal results");
    }

    [Test]
    public void VectorDatabase_HNSW_HandlesEmptyDatabase()
    {
        // Search empty database
        var results = _database.Search("test query", 5, SearchAlgorithm.HNSW);
        
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task VectorDatabase_HNSW_HandlesSingleVector()
    {
        _database.Vectors.Add(new Vector("single test document"));
        await _database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
        
        var results = _database.Search("test", 5, SearchAlgorithm.HNSW, 2.0f);
        
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].OriginalText, Is.EqualTo("single test document"));
    }
}