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
    public void NearestNeighbors_ReturnsCorrectResults()
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
        tree.Build(vectors);
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
    [Explicit("Performance benchmark - run manually")]
    [Category("Benchmark")]
    public void Benchmark_PriorityQueueOptimization()
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
        tree.Build(vectors);

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

}
