using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Neighborly;
using Neighborly.Search;

namespace Tests;

[TestFixture]
[Category("Benchmark")]
public class HNSWBenchmarkTests
{
    private VectorList _vectors = null!;
    private SearchService _searchService = null!;
    private List<Vector> _testVectors = null!;

    [SetUp]
    public void SetUp()
    {
        _vectors = new VectorList();
        _searchService = new SearchService(_vectors);
        _testVectors = new List<Vector>();
    }

    [TearDown]
    public void TearDown()
    {
        _vectors?.Dispose();
    }

    private void CreateTestDataset(int count, int dimensions)
    {
        var random = new Random(42); // Fixed seed for reproducible results
        
        for (int i = 0; i < count; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1); // Range [-1, 1]
            }
            
            var vector = new Vector(embedding, $"vector_{i}");
            _vectors.Add(vector);
            _testVectors.Add(vector);
        }
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    public void Benchmark_SearchPerformance_SmallDataset()
    {
        // Small dataset: 1000 vectors, 128 dimensions
        CreateTestDataset(1000, 128);
        
        var results = BenchmarkSearchAlgorithms(10); // 10 search queries
        
        Console.WriteLine("=== Small Dataset (1000 vectors, 128D) ===");
        PrintBenchmarkResults(results);
        
        // HNSW should be competitive even on small datasets
        Assert.That(results["HNSW"].AverageTime, Is.LessThan(1000), "HNSW should complete searches in under 1 second");
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    public void Benchmark_SearchPerformance_MediumDataset()
    {
        // Medium dataset: 10000 vectors, 256 dimensions
        CreateTestDataset(10000, 256);
        
        var results = BenchmarkSearchAlgorithms(10);
        
        Console.WriteLine("=== Medium Dataset (10000 vectors, 256D) ===");
        PrintBenchmarkResults(results);
        
        // HNSW should show significant advantage over tree-based methods
        Assert.That(results["HNSW"].AverageTime, Is.LessThan(results["KDTree"].AverageTime), 
            "HNSW should be faster than KD-Tree on medium datasets");
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    public void Benchmark_IndexBuildTime()
    {
        CreateTestDataset(5000, 128);
        
        var algorithms = new[] { SearchAlgorithm.HNSW, SearchAlgorithm.KDTree, SearchAlgorithm.BallTree };
        var buildTimes = new Dictionary<string, long>();
        
        foreach (var algorithm in algorithms)
        {
            // Clear any existing indexes
            _searchService.Clear();
            
            var stopwatch = Stopwatch.StartNew();
            _searchService.BuildIndexes(algorithm);
            stopwatch.Stop();
            
            buildTimes[algorithm.ToString()] = stopwatch.ElapsedMilliseconds;
        }
        
        Console.WriteLine("=== Index Build Times (5000 vectors, 128D) ===");
        foreach (var kvp in buildTimes.OrderBy(x => x.Value))
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value} ms");
        }
        
        // HNSW build time should be reasonable (under 30 seconds for 5000 vectors)
        Assert.That(buildTimes["HNSW"], Is.LessThan(30000), "HNSW index build should complete in under 30 seconds");
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    public void Benchmark_AccuracyVsSpeed()
    {
        CreateTestDataset(2000, 64);
        
        // Build indexes
        _searchService.BuildIndexes(SearchAlgorithm.HNSW);
        _searchService.BuildIndexes(SearchAlgorithm.Linear); // Ground truth
        
        var random = new Random(42);
        var queryCount = 20;
        var k = 10;
        
        var hnswAccuracies = new List<double>();
        var hnswTimes = new List<long>();
        
        for (int i = 0; i < queryCount; i++)
        {
            // Generate random query
            var queryEmbedding = new float[64];
            for (int j = 0; j < 64; j++)
            {
                queryEmbedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            var query = new Vector(queryEmbedding);
            
            // Get ground truth from linear search
            var groundTruth = _searchService.Search(query, k, SearchAlgorithm.Linear, 2.0f)
                .Select(v => v.Id)
                .ToHashSet();
            
            // Time HNSW search
            var stopwatch = Stopwatch.StartNew();
            var hnswResults = _searchService.Search(query, k, SearchAlgorithm.HNSW, 2.0f);
            stopwatch.Stop();
            
            hnswTimes.Add(stopwatch.ElapsedMilliseconds);
            
            // Calculate recall (accuracy)
            var hnswIds = hnswResults.Select(v => v.Id).ToHashSet();
            var intersection = groundTruth.Intersect(hnswIds).Count();
            var recall = groundTruth.Count > 0 ? (double)intersection / groundTruth.Count : 1.0;
            hnswAccuracies.Add(recall);
        }
        
        var avgRecall = hnswAccuracies.Average();
        var avgTime = hnswTimes.Average();
        
        Console.WriteLine("=== HNSW Accuracy vs Speed ===");
        Console.WriteLine($"Average Recall: {avgRecall:F3} ({avgRecall * 100:F1}%)");
        Console.WriteLine($"Average Search Time: {avgTime:F2} ms");
        
        // HNSW should achieve reasonable recall (>80%) while being fast
        Assert.That(avgRecall, Is.GreaterThan(0.8), "HNSW should achieve >80% recall");
        Assert.That(avgTime, Is.LessThan(100), "HNSW searches should complete in under 100ms");
    }

    private Dictionary<string, BenchmarkResult> BenchmarkSearchAlgorithms(int queryCount)
    {
        var algorithms = new[]
        {
            SearchAlgorithm.HNSW,
            SearchAlgorithm.KDTree,
            SearchAlgorithm.BallTree,
            SearchAlgorithm.Linear
        };
        
        var results = new Dictionary<string, BenchmarkResult>();
        var random = new Random(42);
        
        // Build all indexes
        foreach (var algorithm in algorithms)
        {
            if (algorithm != SearchAlgorithm.Linear) // Linear doesn't need index
            {
                _searchService.BuildIndexes(algorithm);
            }
        }
        
        foreach (var algorithm in algorithms)
        {
            var times = new List<long>();
            var resultCounts = new List<int>();
            
            for (int i = 0; i < queryCount; i++)
            {
                // Generate random query with same dimensions as test data
                var dimensions = _testVectors[0].Values.Length;
                var queryEmbedding = new float[dimensions];
                for (int j = 0; j < dimensions; j++)
                {
                    queryEmbedding[j] = (float)(random.NextDouble() * 2 - 1);
                }
                var query = new Vector(queryEmbedding);
                
                // Time the search
                var stopwatch = Stopwatch.StartNew();
                var searchResults = _searchService.Search(query, 10, algorithm, 2.0f);
                stopwatch.Stop();
                
                times.Add(stopwatch.ElapsedMilliseconds);
                resultCounts.Add(searchResults.Count);
            }
            
            results[algorithm.ToString()] = new BenchmarkResult
            {
                AverageTime = times.Average(),
                MinTime = times.Min(),
                MaxTime = times.Max(),
                AverageResultCount = resultCounts.Average()
            };
        }
        
        return results;
    }
    
    private void PrintBenchmarkResults(Dictionary<string, BenchmarkResult> results)
    {
        Console.WriteLine($"{"Algorithm",-12} {"Avg Time",-10} {"Min Time",-10} {"Max Time",-10} {"Avg Results",-12}");
        Console.WriteLine(new string('-', 60));
        
        foreach (var kvp in results.OrderBy(x => x.Value.AverageTime))
        {
            var result = kvp.Value;
            Console.WriteLine($"{kvp.Key,-12} {result.AverageTime:F2} ms     {result.MinTime} ms       {result.MaxTime} ms       {result.AverageResultCount:F1}");
        }
        
        // Calculate speedup vs linear search
        if (results.ContainsKey("Linear") && results.ContainsKey("HNSW"))
        {
            var speedup = results["Linear"].AverageTime / results["HNSW"].AverageTime;
            Console.WriteLine($"\nHNSW Speedup vs Linear: {speedup:F1}x");
        }
    }
    
    private class BenchmarkResult
    {
        public double AverageTime { get; set; }
        public long MinTime { get; set; }
        public long MaxTime { get; set; }
        public double AverageResultCount { get; set; }
    }
}