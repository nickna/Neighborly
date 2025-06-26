using Neighborly;
using Neighborly.Distance;
using System.Diagnostics;

namespace CacheOptimizationExample;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Cache Optimization Example");
        Console.WriteLine("==========================\n");

        // Check hardware support
        CheckHardwareSupport();
        
        // Example 1: Basic usage
        await BasicUsageExample();
        
        // Example 2: Batch processing
        await BatchProcessingExample();
        
        // Example 3: Vector pooling
        await VectorPoolingExample();
        
        // Example 4: Performance comparison
        await PerformanceComparisonExample();
    }

    static void CheckHardwareSupport()
    {
        Console.WriteLine("Hardware Support:");
        Console.WriteLine($"- AVX: {System.Runtime.Intrinsics.X86.Avx.IsSupported}");
        Console.WriteLine($"- SSE: {System.Runtime.Intrinsics.X86.Sse.IsSupported}");
        Console.WriteLine($"- Cache Line Size: 64 bytes");
        Console.WriteLine();
    }

    static async Task BasicUsageExample()
    {
        Console.WriteLine("Example 1: Basic Usage");
        Console.WriteLine("---------------------");
        
        // Create vectors
        float[] values1 = Enumerable.Range(0, 128).Select(i => (float)i / 128).ToArray();
        float[] values2 = Enumerable.Range(0, 128).Select(i => (float)(127 - i) / 128).ToArray();
        
        var vector1 = new Vector(values1, "Vector 1");
        var vector2 = new Vector(values2, "Vector 2");
        
        // Regular distance calculation
        var regularCalc = new EuclideanDistanceCalculator();
        var regularDistance = regularCalc.CalculateDistance(vector1, vector2);
        
        // Cache-optimized distance calculation
        using var opt1 = CacheOptimizedVector.FromVector(vector1);
        using var opt2 = CacheOptimizedVector.FromVector(vector2);
        var optimizedCalc = CacheOptimizedEuclideanDistance.Instance;
        var optimizedDistance = optimizedCalc.CalculateDistance(opt1, opt2);
        
        Console.WriteLine($"Regular Distance: {regularDistance:F6}");
        Console.WriteLine($"Optimized Distance: {optimizedDistance:F6}");
        Console.WriteLine($"Difference: {Math.Abs(regularDistance - optimizedDistance):E2}");
        Console.WriteLine();
        
        await Task.CompletedTask;
    }

    static async Task BatchProcessingExample()
    {
        Console.WriteLine("Example 2: Batch Processing");
        Console.WriteLine("--------------------------");
        
        // Create a batch of vectors
        var vectors = new List<Vector>();
        var random = new Random(42);
        
        for (int i = 0; i < 1000; i++)
        {
            float[] values = new float[256];
            for (int j = 0; j < values.Length; j++)
            {
                values[j] = (float)random.NextDouble();
            }
            vectors.Add(new Vector(values, $"Vector_{i}"));
        }
        
        // Create query vector
        float[] queryValues = new float[256];
        for (int i = 0; i < queryValues.Length; i++)
        {
            queryValues[i] = 0.5f; // Center point
        }
        var queryVector = new Vector(queryValues, "Query");
        
        // Process batch with optimization
        var sw = Stopwatch.StartNew();
        float[] distances = queryVector.CacheOptimizedBatchDistance(vectors);
        sw.Stop();
        
        // Find top 5 nearest
        var top5 = distances
            .Select((dist, idx) => (Distance: dist, Index: idx))
            .OrderBy(x => x.Distance)
            .Take(5)
            .ToList();
        
        Console.WriteLine($"Processed {vectors.Count} vectors in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine("Top 5 nearest vectors:");
        foreach (var (distance, index) in top5)
        {
            Console.WriteLine($"  - Vector_{index}: {distance:F4}");
        }
        Console.WriteLine();
        
        await Task.CompletedTask;
    }

    static async Task VectorPoolingExample()
    {
        Console.WriteLine("Example 3: Vector Pooling");
        Console.WriteLine("------------------------");
        
        // Get shared pool for dimension 512
        var pool = CacheOptimizedVectorPool.GetSharedPool(512);
        Console.WriteLine($"Pool created for dimension: {pool.Dimension}");
        
        // Simulate multiple operations with pooling
        var tasks = new List<Task>();
        var random = new Random(42);
        
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    // Generate random vector
                    float[] values = new float[512];
                    for (int k = 0; k < values.Length; k++)
                    {
                        values[k] = (float)random.NextDouble();
                    }
                    
                    // Rent from pool
                    using var pooledVector = pool.Rent(values, $"Task_{taskId}_Vector_{j}");
                    
                    // Use the vector
                    var span = pooledVector.Vector.GetValues();
                    float sum = 0;
                    for (int k = 0; k < span.Length; k++)
                    {
                        sum += span[k];
                    }
                    
                    // Vector automatically returns to pool when disposed
                }
            }));
        }
        
        await Task.WhenAll(tasks);
        
        Console.WriteLine($"Pool size after operations: {pool.CurrentPoolSize}/{pool.MaxPoolSize}");
        Console.WriteLine();
    }

    static async Task PerformanceComparisonExample()
    {
        Console.WriteLine("Example 4: Performance Comparison");
        Console.WriteLine("--------------------------------");
        
        const int vectorCount = 5000;
        const int dimension = 768; // Common embedding dimension
        const int iterations = 5;
        
        // Generate test data
        var random = new Random(42);
        var vectors = new List<Vector>(vectorCount);
        
        for (int i = 0; i < vectorCount; i++)
        {
            float[] values = new float[dimension];
            for (int j = 0; j < dimension; j++)
            {
                values[j] = (float)(random.NextDouble() * 2 - 1);
            }
            vectors.Add(new Vector(values));
        }
        
        float[] queryValues = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            queryValues[i] = (float)(random.NextDouble() * 2 - 1);
        }
        var queryVector = new Vector(queryValues);
        
        // Warm up
        _ = vectors[0].Distance(vectors[1]);
        _ = queryVector.CacheOptimizedDistance(vectors[0]);
        
        // Test 1: Regular distance calculations
        var sw = Stopwatch.StartNew();
        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var vec in vectors)
            {
                _ = queryVector.Distance(vec);
            }
        }
        sw.Stop();
        var regularTime = sw.ElapsedMilliseconds;
        
        // Test 2: Cache-optimized distance calculations
        sw.Restart();
        using (var queryOpt = CacheOptimizedVector.FromVector(queryVector))
        {
            var calculator = CacheOptimizedEuclideanDistance.Instance;
            for (int iter = 0; iter < iterations; iter++)
            {
                foreach (var vec in vectors)
                {
                    using var vecOpt = CacheOptimizedVector.FromVector(vec);
                    _ = calculator.CalculateDistance(queryOpt, vecOpt);
                }
            }
        }
        sw.Stop();
        var optimizedTime = sw.ElapsedMilliseconds;
        
        // Test 3: Batch optimized
        sw.Restart();
        for (int iter = 0; iter < iterations; iter++)
        {
            _ = queryVector.CacheOptimizedBatchDistance(vectors);
        }
        sw.Stop();
        var batchTime = sw.ElapsedMilliseconds;
        
        Console.WriteLine($"Processing {vectorCount} vectors of dimension {dimension}, {iterations} iterations:");
        Console.WriteLine($"- Regular:         {regularTime}ms");
        Console.WriteLine($"- Cache-Optimized: {optimizedTime}ms ({(double)regularTime / optimizedTime:F2}x speedup)");
        Console.WriteLine($"- Batch-Optimized: {batchTime}ms ({(double)regularTime / batchTime:F2}x speedup)");
        Console.WriteLine();
        
        // Memory usage comparison
        long beforeGC = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long afterGC = GC.GetTotalMemory(true);
        
        Console.WriteLine($"Memory usage:");
        Console.WriteLine($"- Before GC: {beforeGC / 1024 / 1024:F2} MB");
        Console.WriteLine($"- After GC:  {afterGC / 1024 / 1024:F2} MB");
        
        await Task.CompletedTask;
    }
}