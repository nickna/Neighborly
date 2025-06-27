using NUnit.Framework;
using System.Threading.Tasks;
using Neighborly;
using Neighborly.Search;

namespace Tests
{
    [TestFixture]
    public class HNSWAsyncTest
    {
        [Test]
        public async Task HNSW_AsyncBuild_WithFloatArrays_CompletesQuickly()
        {
            using var database = new VectorDatabase();
            
            // Add test data using float arrays (no expensive ML.NET embedding)
            database.Vectors.Add(new Vector(new float[] { 1.0f, 2.0f, 3.0f }));
            database.Vectors.Add(new Vector(new float[] { 4.0f, 5.0f, 6.0f }));
            database.Vectors.Add(new Vector(new float[] { 7.0f, 8.0f, 9.0f }));
            
            // Build HNSW index asynchronously
            await database.RebuildSearchIndexAsync(SearchAlgorithm.HNSW);
            
            // Search to verify index works
            var query = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
            var results = database.Search(query, 2, SearchAlgorithm.HNSW);
            
            Assert.That(results.Count, Is.GreaterThan(0), "HNSW should return search results");
        }
    }
}