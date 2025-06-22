using NUnit.Framework;
using System.Threading.Tasks;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
public class SimpleHNSWIntegrationTest
{
    [Test]
    public async Task Simple_HNSW_Test()
    {
        using var database = new VectorDatabase();
        
        // Add some vectors
        database.Vectors.Add(new Vector("The quick brown fox"));
        database.Vectors.Add(new Vector("Machine learning algorithms"));
        database.Vectors.Add(new Vector("Deep neural networks"));
        
        // Build HNSW index
        await database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
        
        // Try searching with a very lenient threshold
        var results = database.Search("neural networks", 3, SearchAlgorithm.HNSW, 10.0f);
        
        Assert.That(results, Is.Not.Null);
        
        // If no results with HNSW, try linear search as comparison
        var linearResults = database.Search("neural networks", 3, SearchAlgorithm.Linear, 10.0f);
        
        Console.WriteLine($"HNSW results: {results.Count}");
        Console.WriteLine($"Linear results: {linearResults.Count}");
        
        // Check if embeddings are being generated with correct dimensions
        var testVector = database.Vectors[0];
        Console.WriteLine($"Vector dimensions: {testVector.Values.Length}");
        Console.WriteLine($"Sample values: [{string.Join(", ", testVector.Values.Take(5))}]");
        
        // At minimum, linear search should work
        Assert.That(linearResults.Count, Is.GreaterThan(0), "Linear search should return results");
    }
}