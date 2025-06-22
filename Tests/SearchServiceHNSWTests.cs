using NUnit.Framework;
using System;
using System.Linq;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
public class SearchServiceHNSWTests
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
    public void SearchService_BuildIndex_HNSW_CreatesValidIndex()
    {
        // Add some test vectors
        var vectors = new[]
        {
            new Vector(new[] { 1.0f, 0.0f }, "first"),
            new Vector(new[] { 0.0f, 1.0f }, "second"),
            new Vector(new[] { 1.0f, 1.0f }, "third")
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }

        // Build HNSW index
        Assert.DoesNotThrow(() => _searchService.BuildIndex(SearchAlgorithm.HNSW));
    }

    [Test]
    public void SearchService_Search_HNSW_ReturnsResults()
    {
        // Add test vectors
        var vectors = new[]
        {
            new Vector(new[] { 1.0f, 0.0f }, "right"),
            new Vector(new[] { 0.0f, 1.0f }, "up"),
            new Vector(new[] { 1.0f, 1.0f }, "diagonal"),
            new Vector(new[] { 0.0f, 0.0f }, "origin")
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }

        // Build index
        _searchService.BuildIndex(SearchAlgorithm.HNSW);

        // Search for vectors close to origin
        var query = new Vector(new[] { 0.1f, 0.1f });
        var results = _searchService.Search(query, 2, SearchAlgorithm.HNSW, 1.0f);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(2));
    }

    [Test]
    public void SearchService_Search_HNSW_WithText_ReturnsResults()
    {
        // Add test vectors using text constructor to ensure consistent dimensions
        _vectors.Add(new Vector("test document"));
        _vectors.Add(new Vector("another document"));
        _vectors.Add(new Vector("third document"));

        // Build index
        _searchService.BuildIndex(SearchAlgorithm.HNSW);

        // Search with text
        var results = _searchService.Search("test", 2, SearchAlgorithm.HNSW);

        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
    }

    [Test]
    public void SearchService_BuildAllIndexes_IncludesHNSW()
    {
        // Add a test vector
        _vectors.Add(new Vector(new[] { 1.0f, 2.0f }, "test"));

        // Build all indexes should not throw
        Assert.DoesNotThrow(() => _searchService.BuildAllIndexes());
    }

    [Test]
    public void SearchService_Clear_ClearsHNSW()
    {
        // Add and build
        _vectors.Add(new Vector(new[] { 1.0f, 2.0f }, "test"));
        _searchService.BuildIndex(SearchAlgorithm.HNSW);

        // Clear should not throw
        Assert.DoesNotThrow(() => _searchService.Clear());
        
        // Should be able to search after clear (will return empty results)
        var query = new Vector(new[] { 1.0f, 2.0f });
        var results = _searchService.Search(query, 1, SearchAlgorithm.HNSW);
        
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.EqualTo(0));
    }

    [Test]
    public void SearchService_Search_HNSW_WithSimilarityThreshold_FiltersResults()
    {
        // Add vectors with known distances
        var vectors = new[]
        {
            new Vector(new[] { 0.0f, 0.0f }, "origin"),
            new Vector(new[] { 1.0f, 0.0f }, "close"),      // Distance = 1.0
            new Vector(new[] { 5.0f, 0.0f }, "far")         // Distance = 5.0
        };

        foreach (var vector in vectors)
        {
            _vectors.Add(vector);
        }

        _searchService.BuildIndex(SearchAlgorithm.HNSW);

        var query = new Vector(new[] { 0.0f, 0.0f });
        
        // With strict threshold, should only get close vectors
        var results = _searchService.Search(query, 3, SearchAlgorithm.HNSW, 2.0f);
        
        Assert.That(results.Count, Is.LessThanOrEqualTo(3));
        
        // All results should be within threshold
        foreach (var result in results)
        {
            var distance = result.Distance(query);
            Assert.That(distance, Is.LessThanOrEqualTo(2.0f));
        }
    }
}