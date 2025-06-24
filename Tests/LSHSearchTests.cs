using NUnit.Framework; // Changed from MSTest
using Neighborly;
using Neighborly.Search;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestFixture] // Changed from TestClass
    public class LSHSearchTests
    {
        private LSHConfig _defaultConfig = null!;
        private const int DefaultDimensions = 4;
        private const int DefaultNumTables = 4;
        private const int DefaultHashesPerTable = 3; // Max 30-31 due to int hash key

        [SetUp] // Changed from TestInitialize
        public void Setup() // Changed from TestInitialize
        {
            _defaultConfig = new LSHConfig(
                vectorDimensions: DefaultDimensions,
                numberOfHashTables: DefaultNumTables,
                hashesPerTable: DefaultHashesPerTable,
                seed: 123 // Fixed seed for deterministic tests
            );
        }

        [Test] // Changed from TestMethod
        public void Constructor_InitializesProjectionsAndTables()
        {
            var lsh = new LSHSearch(_defaultConfig);

            // Reflection to access private fields for testing internals
            var projectionVectorsField = typeof(LSHSearch).GetField("_projectionVectors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hashTablesField = typeof(LSHSearch).GetField("_hashTables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.That(projectionVectorsField, Is.Not.Null, "_projectionVectors field not found.");
            Assert.That(hashTablesField, Is.Not.Null, "_hashTables field not found.");

            var projectionVectors = projectionVectorsField.GetValue(lsh) as List<List<float[]>>;
            var hashTables = hashTablesField.GetValue(lsh) as List<Dictionary<int, List<Guid>>>;

            Assert.That(projectionVectors, Is.Not.Null);
            Assert.That(projectionVectors.Count, Is.EqualTo(DefaultNumTables));
            foreach (var tableProjections in projectionVectors)
            {
                Assert.That(tableProjections.Count, Is.EqualTo(DefaultHashesPerTable));
                foreach (var projVector in tableProjections)
                {
                    Assert.That(projVector.Length, Is.EqualTo(DefaultDimensions));
                    // Check if normalized (sum of squares should be close to 1)
                    float normSq = projVector.Sum(x => x * x);
                    Assert.That(Math.Abs(normSq - 1.0f) < 1e-5, Is.True, $"Projection vector not normalized. NormSq: {normSq}");
                }
            }

            Assert.That(hashTables, Is.Not.Null);
            Assert.That(hashTables.Count, Is.EqualTo(DefaultNumTables)); // NUnit syntax
        }

        [Test] // Changed from TestMethod
        public void ComputeHashCode_ProducesConsistentHashes()
        {
            var lsh = new LSHSearch(_defaultConfig);
            var vec = new Vector(new float[] { 1, 2, 3, 4 });

            // Compute hash multiple times for the same table, should be the same
            int hashCode1_table0 = (int)typeof(LSHSearch).GetMethod("ComputeHashCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(lsh, new object[] { vec, 0 })!;
            int hashCode2_table0 = (int)typeof(LSHSearch).GetMethod("ComputeHashCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(lsh, new object[] { vec, 0 })!;
            Assert.That(hashCode2_table0, Is.EqualTo(hashCode1_table0)); // NUnit syntax

            // Hash for a different table should likely be different (though collisions are possible)
             int hashCode1_table1 = (int)typeof(LSHSearch).GetMethod("ComputeHashCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(lsh, new object[] { vec, 1 })!;
             // Note: With small k and L, hash collisions across tables for the same vector are more likely.
             // This test mainly ensures the hash for a *specific* table is deterministic.
        }

        [Test] // Changed from TestMethod
        public void ComputeHashCode_SimilarVectorsCollideMoreOften()
        {
            // This test is probabilistic and might occasionally fail with bad luck,
            // especially with small k. A more robust test would run many trials.
            var config = new LSHConfig(vectorDimensions: 2, numberOfHashTables: 10, hashesPerTable: 5, seed: 42);
            var lsh = new LSHSearch(config);

            var vec1 = new Vector(new float[] { 1.0f, 0.0f }); // Normalized
            var vec2_similar = new Vector(new float[] { 0.9f, 0.1f }); // Similar
            Normalize(vec2_similar.Values);
            var vec3_dissimilar = new Vector(new float[] { -1.0f, 0.0f }); // Opposite, Dissimilar
            Normalize(vec3_dissimilar.Values);

            int similarCollisions = 0;
            int dissimilarCollisions = 0;

            for (int tableIndex = 0; tableIndex < config.NumberOfHashTables; tableIndex++)
            {
                int hash1 = (int)typeof(LSHSearch).GetMethod("ComputeHashCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(lsh, new object[] { vec1, tableIndex })!;
                int hash2_similar = (int)typeof(LSHSearch).GetMethod("ComputeHashCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(lsh, new object[] { vec2_similar, tableIndex })!;
                int hash3_dissimilar = (int)typeof(LSHSearch).GetMethod("ComputeHashCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(lsh, new object[] { vec3_dissimilar, tableIndex })!;

                if (hash1 == hash2_similar)
                    similarCollisions++;
                if (hash1 == hash3_dissimilar)
                    dissimilarCollisions++;
            }

            Console.WriteLine($"Vec1 CosineSimilarity Vec2_similar: {new Neighborly.Distance.CosineSimilarityCalculator().CalculateDistance(vec1, vec2_similar)}");
            Console.WriteLine($"Vec1 CosineSimilarity Vec3_dissimilar: {new Neighborly.Distance.CosineSimilarityCalculator().CalculateDistance(vec1, vec3_dissimilar)}");
            Console.WriteLine($"Collisions with similar vector: {similarCollisions}/{config.NumberOfHashTables}");
            Console.WriteLine($"Collisions with dissimilar vector: {dissimilarCollisions}/{config.NumberOfHashTables}");

            Assert.That(similarCollisions > dissimilarCollisions, Is.True, "Expected similar vectors to collide more often than dissimilar ones.");
        }


        [Test] // Changed from TestMethod
        public void Build_PopulatesHashTables()
        {
            var lsh = new LSHSearch(_defaultConfig);
            var vectors = new VectorList
            {
                new Vector(new float[] { 1, 1, 1, 1 }),
                new Vector(new float[] { -1, -1, -1, -1 }),
                new Vector(new float[] { 1, 2, 3, 4.5f })
            };

            lsh.Build(vectors);

            var hashTablesField = typeof(LSHSearch).GetField("_hashTables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hashTables = hashTablesField.GetValue(lsh) as List<Dictionary<int, List<Guid>>>;

            Assert.That(hashTables, Is.Not.Null);
            bool foundEntry = false;
            foreach (var table in hashTables)
            {
                if (table.Count > 0)
                {
                    foundEntry = true;
                    // Check if Guids from our vectors are in the buckets
                    var allGuidsInTable = table.Values.SelectMany(list => list).ToHashSet();
                    foreach(var vec in vectors)
                    {
                        Assert.That(allGuidsInTable.Contains(vec.Id), Is.True, $"Vector {vec.Id} not found in any bucket of a populated table.");
                    }
                    break;
                }
            }
            Assert.That(foundEntry, Is.True, "No hash table entries were created after build.");
        }

        [Test] // Changed from TestMethod
        public void Search_ReturnsApproximateNearestNeighbors()
        {
            var lsh = new LSHSearch(_defaultConfig);
            var v1 = new Vector(new float[] { 1, 1, 1, 1 });
            var v2 = new Vector(new float[] { 1.1f, 1.1f, 1.1f, 1.1f }); // very similar to v1
            var v3 = new Vector(new float[] { -1, -1, -1, -1 });      // dissimilar to v1
            var v4 = new Vector(new float[] { 5, 5, 5, 5 });          // somewhat similar to v1, less than v2
            var v5 = new Vector(new float[] { 1, 1, 0, 0 });          // somewhat similar

            var vectors = new VectorList { v1, v2, v3, v4, v5 };
            lsh.Build(vectors);

            var query = new Vector(new float[] { 1, 1, 1, 0.9f }); // Most similar to v1 and v2
            var results = lsh.Search(query, 2);

            Assert.That(results.Count, Is.EqualTo(2), "Should return k results if available.");
            // Exact order depends on LSH luck, but v1 or v2 should be in top results
            Assert.That(results.Contains(v1) || results.Contains(v2), Is.True, "Expected v1 or v2 in the top results.");

            // Check that dissimilar vector v3 is not in the top results (highly likely)
            Assert.That(results.Contains(v3), Is.False, "Dissimilar vector v3 should not be among the top K results.");

            Console.WriteLine("Query distances:");
            var cosineCalc = new Neighborly.Distance.CosineSimilarityCalculator();
            foreach (var vec in vectors)
            {
                Console.WriteLine($"  To {vec.Id} ({string.Join(",",vec.Values)}): {1- cosineCalc.CalculateDistance(query, vec):F4}");
            }
            Console.WriteLine("Search results:");
            foreach (var res in results)
            {
                Console.WriteLine($"  {res.Id} ({string.Join(",",res.Values)}), Distance: {1- cosineCalc.CalculateDistance(query, res):F4}");
            }
        }

        [Test] // Changed from TestMethod
        public void Search_EmptyIndex_ReturnsEmptyList()
        {
            var lsh = new LSHSearch(_defaultConfig);
            var vectors = new VectorList(); // Empty list
            lsh.Build(vectors);

            var query = new Vector(new float[] { 1, 2, 3, 4 });
            var results = lsh.Search(query, 3);

            Assert.That(results.Count, Is.EqualTo(0)); // NUnit syntax
        }

        [Test] // Changed from TestMethod
        public void Build_ThrowsOnDimensionMismatch()
        {
            var config = new LSHConfig(vectorDimensions: 3); // Expects 3D
            var lsh = new LSHSearch(config);
            var vectors = new VectorList { new Vector(new float[] { 1, 2, 3, 4 }) }; // Provides 4D
            Assert.Throws<ArgumentException>(() => lsh.Build(vectors)); // NUnit syntax for expecting exceptions
        }

        [Test] // Changed from TestMethod
        public void Search_ThrowsOnQueryDimensionMismatch()
        {
            var lsh = new LSHSearch(_defaultConfig); // Configured for 4D
            var vectors = new VectorList { new Vector(new float[] { 1,1,1,1})};
            lsh.Build(vectors);
            var query = new Vector(new float[] { 1, 2, 3 }); // Query is 3D
            Assert.Throws<ArgumentException>(() => lsh.Search(query, 1)); // NUnit syntax
        }


        private static void Normalize(float[] vector) // Helper for tests
        {
            float norm = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                norm += vector[i] * vector[i];
            }
            norm = (float)Math.Sqrt(norm);
            if (norm > 0)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= norm;
                }
            }
        }

        [Test]
        public void SaveAndLoad_LSHIndex_PreservesFunctionality()
        {
            var config = new LSHConfig(vectorDimensions: DefaultDimensions, numberOfHashTables: 2, hashesPerTable: 3, seed: 42);
            var lshOriginal = new LSHSearch(config);

            var v1 = new Vector(new float[] { 1, 0, 0, 0 });
            var v2 = new Vector(new float[] { 0.9f, 0.1f, 0, 0 }); // Similar to v1
            var v3 = new Vector(new float[] { 0, 1, 0, 0 });   // Different
            var vectors = new VectorList { v1, v2, v3 };
            lshOriginal.Build(vectors);

            // Perform a search on original
            var query = new Vector(new float[] { 0.95f, 0.05f, 0, 0 });
            var resultsOriginal = lshOriginal.Search(query, 2);

            // Save to memory stream
            byte[] savedIndex;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                lshOriginal.Save(writer);
                savedIndex = ms.ToArray();
            }

            Assert.That(savedIndex.Length, Is.GreaterThan(0));

            // Load into a new instance
            LSHSearch lshLoaded;
            using (var ms = new MemoryStream(savedIndex))
            using (var reader = new BinaryReader(ms))
            {
                // When loading via SearchService, it would pass the existing VectorList.
                // For this direct test, we pass the same VectorList.
                lshLoaded = LSHSearch.LoadFromStream(reader, vectors);
            }

            Assert.That(lshLoaded, Is.Not.Null);
            // Basic config check (more detailed config check could be added)
            var loadedConfigField = typeof(LSHSearch).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var loadedConfig = loadedConfigField?.GetValue(lshLoaded) as LSHConfig;
            Assert.That(loadedConfig, Is.Not.Null);
            Assert.That(loadedConfig.VectorDimensions, Is.EqualTo(config.VectorDimensions));
            Assert.That(loadedConfig.NumberOfHashTables, Is.EqualTo(config.NumberOfHashTables));
            Assert.That(loadedConfig.HashesPerTable, Is.EqualTo(config.HashesPerTable));


            // Perform the same search on loaded
            var resultsLoaded = lshLoaded.Search(query, 2);

            Assert.That(resultsLoaded.Count, Is.EqualTo(resultsOriginal.Count));
            for (int i = 0; i < resultsOriginal.Count; i++)
            {
                Assert.That(resultsLoaded[i].Id, Is.EqualTo(resultsOriginal[i].Id), $"Mismatch at index {i}");
            }
        }

        [Test]
        public void SaveAndLoad_LSHIndex_PreservesFunctionality()
        {
            var config = new LSHConfig(vectorDimensions: DefaultDimensions, numberOfHashTables: 2, hashesPerTable: 3, seed: 42);
            var lshOriginal = new LSHSearch(config);

            var v1 = new Vector(new float[] { 1, 0, 0, 0 });
            var v2 = new Vector(new float[] { 0.9f, 0.1f, 0, 0 }); // Similar to v1
            var v3 = new Vector(new float[] { 0, 1, 0, 0 });   // Different
            var vectors = new VectorList { v1, v2, v3 };
            lshOriginal.Build(vectors);

            // Perform a search on original
            var query = new Vector(new float[] { 0.95f, 0.05f, 0, 0 });
            var resultsOriginal = lshOriginal.Search(query, 2);

            // Save to memory stream
            byte[] savedIndex;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                lshOriginal.Save(writer);
                savedIndex = ms.ToArray();
            }

            Assert.That(savedIndex.Length, Is.GreaterThan(0));

            // Load into a new instance
            LSHSearch lshLoaded;
            using (var ms = new MemoryStream(savedIndex))
            using (var reader = new BinaryReader(ms))
            {
                // When loading via SearchService, it would pass the existing VectorList.
                // For this direct test, we pass the same VectorList.
                lshLoaded = LSHSearch.LoadFromStream(reader, vectors);
            }

            Assert.That(lshLoaded, Is.Not.Null);
            // Basic config check (more detailed config check could be added)
            var loadedConfigField = typeof(LSHSearch).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(loadedConfigField, Is.Not.Null, "Could not find private field '_config' via reflection.");
            var loadedConfig = loadedConfigField?.GetValue(lshLoaded) as LSHConfig;
            Assert.That(loadedConfig, Is.Not.Null);
            Assert.That(loadedConfig.VectorDimensions, Is.EqualTo(config.VectorDimensions));
            Assert.That(loadedConfig.NumberOfHashTables, Is.EqualTo(config.NumberOfHashTables));
            Assert.That(loadedConfig.HashesPerTable, Is.EqualTo(config.HashesPerTable));
            Assert.That(loadedConfig.Seed, Is.EqualTo(config.Seed));


            // Perform the same search on loaded
            var resultsLoaded = lshLoaded.Search(query, 2);

            Assert.That(resultsLoaded.Count, Is.EqualTo(resultsOriginal.Count), "Number of results mismatch after load.");
            for (int i = 0; i < resultsOriginal.Count; i++)
            {
                Assert.That(resultsLoaded[i].Id, Is.EqualTo(resultsOriginal[i].Id), $"Mismatch at search result index {i} after load.");
            }
        }
    }
}
