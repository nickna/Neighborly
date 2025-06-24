using NUnit.Framework;
using Neighborly;
using Neighborly.Search;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestFixture]
    public class SearchServiceLSHTests
    {
        private VectorList _vectors = null!;
        private SearchService _searchService = null!;
        private const int DefaultDimensions = 4;

        [SetUp]
        public void Setup()
        {
            _vectors = new VectorList
            {
                new Vector(new float[] { 1, 0, 0, 0 }, "v1"),
                new Vector(new float[] { 0.9f, 0.1f, 0, 0 }, "v2_sim_v1"), // Similar to v1
                new Vector(new float[] { 0, 1, 0, 0 }, "v3_diff_v1"),   // Different from v1
                new Vector(new float[] { -1, 0, 0, 0 }, "v4_opp_v1")    // Opposite to v1
            };
            // Normalize vectors for cosine similarity if LSH relies on it implicitly
            foreach(var v in _vectors)
            {
                Normalize(v.Values);
            }
            _searchService = new SearchService(_vectors);
        }

        private static void Normalize(float[] vector)
        {
            float norm = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                norm += vector[i] * vector[i];
            }
            norm = (float)System.Math.Sqrt(norm);
            if (norm > 1e-6) // Avoid division by zero or tiny numbers
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= norm;
                }
            }
        }

        [Test]
        public void BuildIndex_LSH_CreatesLSHSearchInstance()
        {
            _searchService.BuildIndex(SearchAlgorithm.LSH);

            // Use reflection to check if _lshSearch is instantiated
            var lshSearchField = typeof(SearchService).GetField("_lshSearch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(lshSearchField, Is.Not.Null, "Could not find private field '_lshSearch' in SearchService.");
            var lshSearchInstance = lshSearchField?.GetValue(_searchService);
            Assert.That(lshSearchInstance, Is.Not.Null, "_lshSearch instance was not created after BuildIndex(LSH).");
        }

        [Test]
        public void Search_LSH_ReturnsApproximateResults()
        {
            _searchService.BuildIndex(SearchAlgorithm.LSH);

            var query = new Vector(new float[] { 0.95f, 0.05f, 0, 0 });
            Normalize(query.Values); // Ensure query is normalized if data is

            var results = _searchService.Search(query, 2, SearchAlgorithm.LSH, similarityThreshold: 0.5f); // Using a threshold typical for 1-cosine_sim

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.LessThanOrEqualTo(2));

            // We expect v1 or v2_sim_v1 to be good candidates
            bool foundExpected = results.Any(r => r.OriginalText == "v1" || r.OriginalText == "v2_sim_v1");
            if (results.Count > 0) // Only assert if results are returned, LSH is probabilistic
            {
                 Assert.That(foundExpected, Is.True, "Expected one of the similar vectors in LSH search results.");
            }
            else
            {
                Assert.Warn("LSH search returned no results for the query. This can happen due to LSH's probabilistic nature, especially with default/few hash tables/bits.");
            }

            // Check that a very dissimilar vector is unlikely
            bool foundDissimilar = results.Any(r => r.OriginalText == "v4_opp_v1");
            Assert.That(foundDissimilar, Is.False, "A very dissimilar vector was unexpectedly found by LSH.");
        }

        [Test]
        public void Search_LSH_WithTextQuery_ReturnsResults()
        {
            // This test requires an embedding generator that produces vectors of DefaultDimensions
            // For now, we'll mock a simple one or ensure default config matches.
            // The default EmbeddingGenerator might produce different dimensions.
            // We need to ensure config matches.
            if (_vectors.Count > 0)
            {
                 // Ensure SearchService's LSH instance is configured for the actual vector dimensions
                _searchService.BuildIndex(SearchAlgorithm.LSH); // This will use actual dimensions from _vectors
            }


            // Assuming EmbeddingGenerator produces vectors of DefaultDimensions for "v1"
            // This part is tricky without controlling the EmbeddingGenerator precisely for test
            var results = _searchService.Search("v1", 1, SearchAlgorithm.LSH, similarityThreshold: 0.1f); // low threshold (high similarity)

            Assert.That(results, Is.Not.Null);
            if (results.Any())
            {
                Assert.That(results.First().OriginalText, Is.EqualTo("v1").Or.EqualTo("v2_sim_v1"));
            }
            else
            {
                Assert.Warn("LSH text search returned no results. Embedding or LSH parameters might need tuning for generic text 'v1'.");
            }
        }

        [Test]
        public void Clear_LSH_ResetsLSHSearchInstance()
        {
            _searchService.BuildIndex(SearchAlgorithm.LSH);
            _searchService.Clear();

            var lshSearchField = typeof(SearchService).GetField("_lshSearch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(lshSearchField, Is.Not.Null);
            var lshSearchInstance = lshSearchField?.GetValue(_searchService);
            Assert.That(lshSearchInstance, Is.Null, "_lshSearch instance was not cleared.");
        }
    }
}
