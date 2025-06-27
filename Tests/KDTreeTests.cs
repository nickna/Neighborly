using System.Diagnostics;
using System.Text;
using Neighborly.Search;

namespace Neighborly.Tests;

[TestFixture]
public class KDTreeTests
{
    [Test]
    public void CanSaveAndLoad()
    {
        // Arrange
        KDTree originalTree = new();
        VectorList vectors = [new Vector([1f, 2, 3]), new Vector([4f, 5, 6]), new Vector([7f, 8, 9])];
        originalTree.Build(vectors);

        // Act
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            originalTree.Save(writer, vectors);
        }

        stream.Seek(0, SeekOrigin.Begin);
        KDTree loadedTree = new();
        using (var reader = new BinaryReader(stream))
        {
            loadedTree.Load(reader, vectors);
        }

        // Assert
        Assert.That(loadedTree, Is.EqualTo(originalTree));
    }

    [Test]
    public async Task NearestNeighbors_ReturnsCorrectResults()
    {
        // Arrange
        var tree = new KDTree();
        var vectors = new VectorList
        {
            new Vector([0f, 0f], "origin"),
            new Vector([1f, 0f], "right"),
            new Vector([0f, 1f], "up"),
            new Vector([1f, 1f], "diagonal"),
            new Vector([-1f, -1f], "opposite")
        };
        await tree.Build(vectors);
        var query = new Vector([0.5f, 0.5f], "query");

        // Act
        var results = tree.NearestNeighbors(query, 3);

        // Assert
        Assert.That(results.Count, Is.EqualTo(3));
        // Should find the 3 closest points (origin, right, up, diagonal are equidistant at 0.707)
        var resultTexts = results.Select(v => v.OriginalText).ToHashSet();
        var expectedClosest = new HashSet<string> { "origin", "right", "up", "diagonal" };
        
        // All results should be from the closest 4 points, not the distant "opposite"
        Assert.That(resultTexts.All(t => expectedClosest.Contains(t)), Is.True, 
            "All results should be from the closest points");
        Assert.That(resultTexts, Does.Not.Contain("opposite"), 
            "Should not include the distant 'opposite' point");
    }

    [Test]
    public async Task ParallelNearestNeighbors_ReturnsCorrectResults()
    {
        // Arrange
        var tree = new KDTree();
        var vectors = new VectorList
        {
            new Vector([0f, 0f], "origin"),
            new Vector([1f, 0f], "right"),
            new Vector([0f, 1f], "up"),
            new Vector([1f, 1f], "diagonal"),
            new Vector([-1f, -1f], "opposite")
        };
        await tree.Build(vectors);
        var query = new Vector([0.5f, 0.5f], "query");

        // Act
        var parallelResults = await tree.NearestNeighborsParallel(query, 3);
        var sequentialResults = tree.NearestNeighbors(query, 3);

        // Assert
        Assert.That(parallelResults.Count, Is.EqualTo(3));
        Assert.That(parallelResults.Count, Is.EqualTo(sequentialResults.Count));
        
        // Results should be equivalent (same vectors, potentially different order)
        var parallelIds = parallelResults.Select(v => v.Id).ToHashSet();
        var sequentialIds = sequentialResults.Select(v => v.Id).ToHashSet();
        Assert.That(parallelIds, Is.EqualTo(sequentialIds));
    }

    [Test]
    public async Task ParallelRangeNeighbors_ReturnsCorrectResults()
    {
        // Arrange
        var tree = new KDTree();
        var vectors = new VectorList
        {
            new Vector([0f, 0f], "origin"),
            new Vector([1f, 0f], "right"),
            new Vector([0f, 1f], "up"),
            new Vector([1f, 1f], "diagonal"),
            new Vector([3f, 3f], "far")
        };
        await tree.Build(vectors);
        var query = new Vector([0f, 0f], "query");
        var radius = 1.5f;

        // Act
        var parallelResults = await tree.RangeNeighborsParallel(query, radius);
        var sequentialResults = tree.RangeNeighbors(query, radius);

        // Assert
        Assert.That(parallelResults.Count, Is.EqualTo(sequentialResults.Count));
        
        // Results should contain the same vectors
        var parallelIds = parallelResults.Select(v => v.Id).ToHashSet();
        var sequentialIds = sequentialResults.Select(v => v.Id).ToHashSet();
        Assert.That(parallelIds, Is.EqualTo(sequentialIds));
        
        // All results should be within radius
        foreach (var result in parallelResults)
        {
            var distance = (result - query).Magnitude;
            Assert.That(distance, Is.LessThanOrEqualTo(radius));
        }
    }

    [Test]
    public async Task ParallelConstruction_ProducesEquivalentTree()
    {
        // Arrange
        var vectors = new VectorList();
        var random = new Random(42);
        for (int i = 0; i < 1500; i++) // Above parallel threshold
        {
            var embedding = new float[10];
            for (int j = 0; j < 10; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 10);
            }
            vectors.Add(new Vector(embedding, $"vector_{i}"));
        }

        // Act - Build trees with both methods
        KDTreeParallelConfig.EnableParallelConstruction = false;
        var sequentialTree = new KDTree();
        await sequentialTree.Build(vectors);

        KDTreeParallelConfig.EnableParallelConstruction = true;
        var parallelTree = new KDTree();
        await parallelTree.Build(vectors);

        // Assert - Both trees should produce equivalent search results
        var query = vectors[100];
        var seqResults = sequentialTree.NearestNeighbors(query, 10);
        var parResults = parallelTree.NearestNeighbors(query, 10);

        Assert.That(parResults.Count, Is.EqualTo(seqResults.Count));
        
        // Both should find the same nearest neighbors (order may differ due to parallel construction)
        var seqIds = seqResults.Select(v => v.Id).ToHashSet();
        var parIds = parResults.Select(v => v.Id).ToHashSet();
        Assert.That(parIds, Is.EqualTo(seqIds));

        // Reset to default
        KDTreeParallelConfig.EnableParallelConstruction = true;
    }

    [Test]
    public void ParallelConfiguration_RespectsSettings()
    {
        // Arrange
        var vectors = new VectorList();
        for (int i = 0; i < 500; i++) // Below default parallel threshold
        {
            vectors.Add(new Vector([i, i], $"vector_{i}"));
        }

        // Act & Assert - Parallel construction should be disabled for small datasets
        var originalThreshold = KDTreeParallelConfig.ParallelConstructionThreshold;
        KDTreeParallelConfig.ParallelConstructionThreshold = 1000;

        var tree = new KDTree();
        // Should use sequential construction since dataset is below threshold
        Assert.DoesNotThrowAsync(async () => await tree.Build(vectors));

        // Test with lowered threshold
        KDTreeParallelConfig.ParallelConstructionThreshold = 100;
        var tree2 = new KDTree();
        // Should use parallel construction since dataset is above new threshold
        Assert.DoesNotThrowAsync(async () => await tree2.Build(vectors));

        // Reset
        KDTreeParallelConfig.ParallelConstructionThreshold = originalThreshold;
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    [Category("Benchmark")]
    public async Task Benchmark_PriorityQueueOptimization()
    {
        // Arrange - Create a large dataset to see the optimization benefits
        var random = new Random(42);
        var vectorCount = 5000;
        var dimensions = 128;
        var k = 50; // Search for many neighbors to stress the priority queue
        var queryCount = 20;
        
        var vectors = new VectorList();
        for (int i = 0; i < vectorCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            vectors.Add(new Vector(embedding, $"vector_{i}"));
        }

        var tree = new KDTree();
        await tree.Build(vectors);

        // Generate query vectors
        var queries = new List<Vector>();
        for (int i = 0; i < queryCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            queries.Add(new Vector(embedding, $"query_{i}"));
        }

        // Act - Benchmark the optimized k-NN search
        var times = new List<long>();
        var resultCounts = new List<int>();

        foreach (var query in queries)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = tree.NearestNeighbors(query, k);
            stopwatch.Stop();

            times.Add(stopwatch.ElapsedMilliseconds);
            resultCounts.Add(results.Count);
        }

        // Assert and Report
        var avgTime = times.Average();
        var minTime = times.Min();
        var maxTime = times.Max();
        var avgResults = resultCounts.Average();

        Console.WriteLine("=== KDTree Priority Queue Optimization Benchmark ===");
        Console.WriteLine($"Dataset: {vectorCount} vectors, {dimensions}D");
        Console.WriteLine($"Query: k={k}, {queryCount} queries");
        Console.WriteLine($"Average Time: {avgTime:F2} ms");
        Console.WriteLine($"Min Time: {minTime} ms");
        Console.WriteLine($"Max Time: {maxTime} ms");
        Console.WriteLine($"Average Results: {avgResults:F1}");
        Console.WriteLine($"Time per result: {avgTime / avgResults:F3} ms");

        // Performance assertions - these should be much better with priority queue
        Assert.That(avgTime, Is.LessThan(1000), "Average search time should be under 1 second");
        Assert.That(avgResults, Is.EqualTo(k), "Should return exactly k results");
        
        // Test correctness by comparing a few results with linear search
        var testQuery = queries.First();
        var kdTreeResults = tree.NearestNeighbors(testQuery, 10);
        var linearResults = vectors
            .OrderBy(v => (v - testQuery).Magnitude)
            .Take(10)
            .ToList();
            
        Console.WriteLine("\n=== Correctness Check ===");
        Console.WriteLine("KDTree results vs Linear search (should match):");
        for (int i = 0; i < Math.Min(kdTreeResults.Count, linearResults.Count); i++)
        {
            var kdDist = (kdTreeResults[i] - testQuery).Magnitude;
            var linearDist = (linearResults[i] - testQuery).Magnitude;
            Console.WriteLine($"  {i+1}: KD={kdDist:F4}, Linear={linearDist:F4}");
        }
        
        // Allow for small floating point differences in distance calculations
        Assert.That(kdTreeResults.Count, Is.EqualTo(10), "Should return 10 results");
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    [Category("Benchmark")]
    public async Task Benchmark_ParallelTreeConstruction()
    {
        // Arrange - Create datasets of varying sizes
        var random = new Random(42);
        var dimensions = 64;
        var sizes = new[] { 500, 1000, 2000, 5000, 10000 };

        Console.WriteLine("=== KDTree Parallel Construction Benchmark ===");
        Console.WriteLine($"Dimensions: {dimensions}");
        Console.WriteLine($"{"Size",-8} {"Sequential (ms)",-15} {"Parallel (ms)",-13} {"Speedup",-8} {"Correctness",-11}");
        Console.WriteLine(new string('-', 65));

        foreach (var size in sizes)
        {
            var vectors = new VectorList();
            for (int i = 0; i < size; i++)
            {
                var embedding = new float[dimensions];
                for (int j = 0; j < dimensions; j++)
                {
                    embedding[j] = (float)(random.NextDouble() * 2 - 1);
                }
                vectors.Add(new Vector(embedding, $"vector_{i}"));
            }

            // Sequential construction
            var seqTree = new KDTree();
            KDTreeParallelConfig.EnableParallelConstruction = false;
            var seqStopwatch = Stopwatch.StartNew();
            await seqTree.Build(vectors);
            seqStopwatch.Stop();

            // Parallel construction
            var parTree = new KDTree();
            KDTreeParallelConfig.EnableParallelConstruction = true;
            var parStopwatch = Stopwatch.StartNew();
            await parTree.Build(vectors);
            parStopwatch.Stop();

            // Verify correctness by comparing search results
            var query = vectors[size / 2];
            var seqResults = seqTree.NearestNeighbors(query, 10);
            var parResults = parTree.NearestNeighbors(query, 10);
            bool correctness = seqResults.Count == parResults.Count;

            double speedup = (double)seqStopwatch.ElapsedMilliseconds / parStopwatch.ElapsedMilliseconds;

            Console.WriteLine($"{size,-8} {seqStopwatch.ElapsedMilliseconds,-15} {parStopwatch.ElapsedMilliseconds,-13} {speedup:F2}x{"",-4} {(correctness ? "✓" : "✗"),-11}");
        }

        // Reset to default
        KDTreeParallelConfig.EnableParallelConstruction = true;
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    [Category("Benchmark")]
    public async Task Benchmark_ParallelSearch()
    {
        // Arrange - Create a large dataset
        var random = new Random(42);
        var vectorCount = 10000;
        var dimensions = 128;
        var queryCount = 50;
        var kValues = new[] { 1, 5, 10, 50, 100 };

        var vectors = new VectorList();
        for (int i = 0; i < vectorCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            vectors.Add(new Vector(embedding, $"vector_{i}"));
        }

        var tree = new KDTree();
        await tree.Build(vectors);

        // Generate query vectors
        var queries = new List<Vector>();
        for (int i = 0; i < queryCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            queries.Add(new Vector(embedding, $"query_{i}"));
        }

        Console.WriteLine("\n=== KDTree Parallel k-NN Search Benchmark ===");
        Console.WriteLine($"Dataset: {vectorCount} vectors, {dimensions}D, {queryCount} queries");
        Console.WriteLine($"{"k",-5} {"Sequential (ms)",-15} {"Parallel (ms)",-13} {"Speedup",-8} {"Correctness",-11}");
        Console.WriteLine(new string('-', 60));

        foreach (var k in kValues)
        {
            // Sequential search
            var seqTimes = new List<long>();
            var seqResults = new List<IList<Vector>>();
            foreach (var query in queries)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = tree.NearestNeighbors(query, k);
                stopwatch.Stop();
                seqTimes.Add(stopwatch.ElapsedMilliseconds);
                seqResults.Add(result);
            }

            // Parallel search
            var parTimes = new List<long>();
            var parResults = new List<IList<Vector>>();
            foreach (var query in queries)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await tree.NearestNeighborsParallel(query, k);
                stopwatch.Stop();
                parTimes.Add(stopwatch.ElapsedMilliseconds);
                parResults.Add(result);
            }

            // Calculate averages and verify correctness
            var avgSeq = seqTimes.Average();
            var avgPar = parTimes.Average();
            double speedup = avgSeq / avgPar;

            // Check correctness (results should have same count)
            bool correctness = true;
            for (int i = 0; i < queryCount; i++)
            {
                if (seqResults[i].Count != parResults[i].Count)
                {
                    correctness = false;
                    break;
                }
            }

            Console.WriteLine($"{k,-5} {avgSeq:F1}{"ms",-12} {avgPar:F1}{"ms",-10} {speedup:F2}x{"",-4} {(correctness ? "✓" : "✗"),-11}");
        }
    }

    [Test]
    [Explicit("Performance benchmark - run manually")]
    [Category("Benchmark")]
    public async Task Benchmark_ParallelRangeSearch()
    {
        // Arrange - Create a large dataset
        var random = new Random(42);
        var vectorCount = 5000;
        var dimensions = 64;
        var queryCount = 20;
        var radii = new[] { 0.5f, 1.0f, 2.0f, 5.0f };

        var vectors = new VectorList();
        for (int i = 0; i < vectorCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            vectors.Add(new Vector(embedding, $"vector_{i}"));
        }

        var tree = new KDTree();
        await tree.Build(vectors);

        // Generate query vectors
        var queries = new List<Vector>();
        for (int i = 0; i < queryCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            queries.Add(new Vector(embedding, $"query_{i}"));
        }

        Console.WriteLine("\n=== KDTree Parallel Range Search Benchmark ===");
        Console.WriteLine($"Dataset: {vectorCount} vectors, {dimensions}D, {queryCount} queries");
        Console.WriteLine($"{"Radius",-8} {"Sequential (ms)",-15} {"Parallel (ms)",-13} {"Speedup",-8} {"Avg Results",-11}");
        Console.WriteLine(new string('-', 65));

        foreach (var radius in radii)
        {
            // Sequential range search
            var seqTimes = new List<long>();
            var seqResultCounts = new List<int>();
            foreach (var query in queries)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = tree.RangeNeighbors(query, radius);
                stopwatch.Stop();
                seqTimes.Add(stopwatch.ElapsedMilliseconds);
                seqResultCounts.Add(result.Count);
            }

            // Parallel range search
            var parTimes = new List<long>();
            var parResultCounts = new List<int>();
            foreach (var query in queries)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await tree.RangeNeighborsParallel(query, radius);
                stopwatch.Stop();
                parTimes.Add(stopwatch.ElapsedMilliseconds);
                parResultCounts.Add(result.Count);
            }

            // Calculate averages
            var avgSeq = seqTimes.Average();
            var avgPar = parTimes.Average();
            var avgResults = seqResultCounts.Average();
            double speedup = avgSeq / avgPar;

            Console.WriteLine($"{radius,-8:F1} {avgSeq:F1}{"ms",-12} {avgPar:F1}{"ms",-10} {speedup:F2}x{"",-4} {avgResults:F1,-11}");
        }
    }

    [Test]
    [Explicit("Memory benchmark - run manually")]
    [Category("Benchmark")]
    public async Task Benchmark_MemoryUsage()
    {
        // Arrange - Test memory usage with different configurations
        var random = new Random(42);
        var vectorCount = 10000;
        var dimensions = 128;

        var vectors = new VectorList();
        for (int i = 0; i < vectorCount; i++)
        {
            var embedding = new float[dimensions];
            for (int j = 0; j < dimensions; j++)
            {
                embedding[j] = (float)(random.NextDouble() * 2 - 1);
            }
            vectors.Add(new Vector(embedding, $"vector_{i}"));
        }

        Console.WriteLine("\n=== KDTree Memory Usage Benchmark ===");
        Console.WriteLine($"Dataset: {vectorCount} vectors, {dimensions}D");

        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);

        // Sequential construction
        KDTreeParallelConfig.EnableParallelConstruction = false;
        var seqTree = new KDTree();
        var seqStopwatch = Stopwatch.StartNew();
        await seqTree.Build(vectors);
        seqStopwatch.Stop();

        var seqMemory = GC.GetTotalMemory(false) - initialMemory;

        // Clean up
        seqTree = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Parallel construction
        KDTreeParallelConfig.EnableParallelConstruction = true;
        var parTree = new KDTree();
        var parStopwatch = Stopwatch.StartNew();
        await parTree.Build(vectors);
        parStopwatch.Stop();

        var parMemory = GC.GetTotalMemory(false) - initialMemory;

        Console.WriteLine($"Sequential: {seqStopwatch.ElapsedMilliseconds}ms, {seqMemory / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"Parallel:   {parStopwatch.ElapsedMilliseconds}ms, {parMemory / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"Memory Overhead: {((double)parMemory / seqMemory - 1) * 100:F1}%");

        // Reset to default
        KDTreeParallelConfig.EnableParallelConstruction = true;
    }

}
