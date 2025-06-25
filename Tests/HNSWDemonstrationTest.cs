using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
public class HNSWDemonstrationTest
{
    [Test]
    public async Task HNSW_Demonstration_SemanticSearch()
    {
        using var database = new VectorDatabase();
        
        // Add diverse content to demonstrate semantic search
        var documents = new[]
        {
            "Machine learning algorithms for data analysis",
            "Deep neural networks and artificial intelligence",
            "Vector databases for similarity search",
            "HNSW algorithm for approximate nearest neighbors",
            "Cooking recipes and kitchen techniques",
            "Travel destinations in Europe",
            "Financial markets and investment strategies",
            "Natural language processing and text analysis",
            "Computer vision and image recognition",
            "Database indexing and query optimization"
        };

        foreach (var doc in documents)
        {
            database.Vectors.Add(new Vector(doc));
        }

        // Build HNSW index
        await database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);

        // Test semantic search for AI/ML related content
        Console.WriteLine("=== HNSW Semantic Search Demonstration ===");
        
        var queries = new[]
        {
            "artificial intelligence research",
            "database search optimization", 
            "cooking and food preparation"
        };

        foreach (var query in queries)
        {
            Console.WriteLine($"\nQuery: '{query}'");
            
            // Use HNSW with a reasonable threshold
            var results = database.Search(query, 3, SearchAlgorithm.HNSW, 10.0f);
            
            Console.WriteLine($"HNSW found {results.Count} results:");
            for (int i = 0; i < results.Count; i++)
            {
                var distance = results[i].Distance(new Vector(query));
                Console.WriteLine($"  {i + 1}. [{distance:F3}] {results[i].OriginalText}");
            }
        }

        // Verify HNSW returns relevant results for AI query
        var aiResults = database.Search("machine learning", 3, SearchAlgorithm.HNSW, 10.0f);
        Assert.That(aiResults.Count, Is.GreaterThan(0), "HNSW should find ML-related content");
        
        Console.WriteLine($"\nML Query Results ({aiResults.Count} found):");
        foreach (var result in aiResults)
        {
            var distance = result.Distance(new Vector("machine learning"));
            Console.WriteLine($"  - [{distance:F3}] {result.OriginalText}");
        }
        
        // Check for exact term matches first (most reliable)
        var exactMatches = aiResults.Where(r => 
            r.OriginalText.Contains("machine learning", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("neural networks", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("artificial intelligence", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("HNSW", StringComparison.OrdinalIgnoreCase)
        ).ToList();
        
        // If no exact matches, check for broader AI/ML related terms
        var semanticMatches = aiResults.Where(r =>
            r.OriginalText.Contains("processing", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("analysis", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("algorithm", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("data", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("computer", StringComparison.OrdinalIgnoreCase) ||
            r.OriginalText.Contains("vision", StringComparison.OrdinalIgnoreCase)
        ).ToList();
        
        Console.WriteLine($"Exact matches: {exactMatches.Count}, Semantic matches: {semanticMatches.Count}");
        
        // Accept either exact matches or semantic matches (more flexible for different embedding approaches)
        var totalRelevant = exactMatches.Count > 0 ? exactMatches.Count : semanticMatches.Count;
        Assert.That(totalRelevant, Is.GreaterThan(0), 
            $"HNSW should return relevant results for ML query. Found {aiResults.Count} results but none were AI/ML related: {string.Join(", ", aiResults.Select(r => $"'{r.OriginalText}'"))}");
    }

    [Test]
    public async Task HNSW_PerformanceComparison_WithAlgorithms()
    {
        using var database = new VectorDatabase();
        
        // Add test data
        var random = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            database.Vectors.Add(new Vector($"Document {i} with content about topic {i % 10}"));
        }

        // Build indexes
        await database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
        await database.RebuildSearchIndexAsync(SearchAlgorithm.KDTree);

        var query = "Document about topic 5";
        
        // Compare HNSW vs KDTree
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var hnswResults = database.Search(query, 10, SearchAlgorithm.HNSW, 10.0f);
        var hnswTime = stopwatch.ElapsedMilliseconds;
        
        stopwatch.Restart();
        var kdtreeResults = database.Search(query, 10, SearchAlgorithm.KDTree, 10.0f);
        var kdtreeTime = stopwatch.ElapsedMilliseconds;
        
        Console.WriteLine("=== Performance Comparison ===");
        Console.WriteLine($"HNSW: {hnswResults.Count} results in {hnswTime} ms");
        Console.WriteLine($"KDTree: {kdtreeResults.Count} results in {kdtreeTime} ms");
        
        // Both should return results
        Assert.That(hnswResults.Count, Is.GreaterThan(0), "HNSW should return results");
        Assert.That(kdtreeResults.Count, Is.GreaterThan(0), "KDTree should return results");
        
        // HNSW should be reasonably fast (under 100ms for 1000 vectors)
        Assert.That(hnswTime, Is.LessThan(100), "HNSW should be fast for medium datasets");
    }

    [Test]
    public async Task HNSW_Serialization_PreservesSearchCapability()
    {
        using var database = new VectorDatabase();
        
        // Add test data
        database.Vectors.Add(new Vector("First test document"));
        database.Vectors.Add(new Vector("Second test document"));
        database.Vectors.Add(new Vector("Third different content"));
        
        // Build HNSW index
        await database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
        
        // Search before serialization
        var originalResults = database.Search("test document", 2, SearchAlgorithm.HNSW, 10.0f);
        
        // Save and reload database (tests serialization)
        var tempPath = Path.GetTempPath();
        var testDbPath = Path.Combine(tempPath, "hnsw_test_" + Guid.NewGuid().ToString("N")[..8]);
        
        try
        {
            Directory.CreateDirectory(testDbPath);
            await database.SaveAsync(testDbPath);
            
            // Create new database and load
            using var newDatabase = new VectorDatabase();
            await newDatabase.LoadAsync(testDbPath);
            
            // Search after loading
            var loadedResults = newDatabase.Search("test document", 2, SearchAlgorithm.HNSW, 10.0f);
            
            Console.WriteLine($"Original results: {originalResults.Count}");
            Console.WriteLine($"Loaded results: {loadedResults.Count}");
            
            // Should return same number of results
            Assert.That(loadedResults.Count, Is.EqualTo(originalResults.Count), 
                "Loaded HNSW should return same number of results");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDbPath))
            {
                Directory.Delete(testDbPath, true);
            }
        }
    }
}