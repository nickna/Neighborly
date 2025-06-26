using Neighborly;
using Neighborly.Distance;
using Neighborly.Search;
using System.Diagnostics;

namespace BatchOptimizationExample;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Batch Distance Calculation Optimization Example");
        Console.WriteLine("==============================================\n");

        // Example 1: Basic batch distance calculation
        await BasicBatchExample();
        
        // Example 2: Search algorithm comparison
        await SearchAlgorithmComparison();
        
        // Example 3: Performance comparison
        await PerformanceComparison();
    }

    static async Task BasicBatchExample()
    {
        Console.WriteLine("Example 1: Basic Batch Distance Calculation");
        Console.WriteLine("------------------------------------------");
        
        // Create test vectors
        var random = new Random(42);
        var vectors = new List<Vector>();
        for (int i = 0; i < 100; i++)
        {
            float[] values = Enumerable.Range(0, 128).Select(_ => (float)random.NextDouble()).ToArray();
            vectors.Add(new Vector(values, $"Vector_{i}"));
        }
        
        float[] queryValues = Enumerable.Range(0, 128).Select(_ => (float)random.NextDouble()).ToArray();
        var query = new Vector(queryValues, "Query");
        
        // Single distance calculation
        Console.WriteLine("Calculating distances one by one...");
        var sw = Stopwatch.StartNew();
        var calculator = new EuclideanDistanceCalculator();
        float[] singleResults = new float[vectors.Count];
        for (int i = 0; i < vectors.Count; i++)
        {
            singleResults[i] = calculator.CalculateDistance(query, vectors[i]);
        }
        sw.Stop();
        var singleTime = sw.ElapsedMilliseconds;
        
        // Batch distance calculation
        Console.WriteLine("Calculating distances in batch...");
        sw.Restart();
        var batchCalculator = BatchEuclideanDistanceCalculator.Instance;
        float[] batchResults = batchCalculator.CalculateDistances(query, vectors);
        sw.Stop();
        var batchTime = sw.ElapsedMilliseconds;
        
        Console.WriteLine($"Single calculation time: {singleTime}ms");
        Console.WriteLine($"Batch calculation time: {batchTime}ms");
        Console.WriteLine($"Speedup: {(double)singleTime / Math.Max(1, batchTime):F2}x");
        
        // Verify results match
        bool resultsMatch = true;
        for (int i = 0; i < singleResults.Length; i++)
        {
            if (Math.Abs(singleResults[i] - batchResults[i]) > 1e-5f)
            {
                resultsMatch = false;
                break;
            }
        }
        Console.WriteLine($"Results match: {resultsMatch}");
        Console.WriteLine();
        
        await Task.CompletedTask;
    }

    static async Task SearchAlgorithmComparison()
    {
        Console.WriteLine("Example 2: Search Algorithm Comparison");
        Console.WriteLine("-------------------------------------");
        
        // Create a larger dataset
        var random = new Random(42);
        var vectorList = new VectorList();
        
        for (int i = 0; i < 1000; i++)
        {
            float[] values = Enumerable.Range(0, 256).Select(_ => (float)random.NextDouble()).ToArray();
            vectorList.Add(new Vector(values, $"Vector_{i}"));
        }
        
        float[] queryValues = Enumerable.Range(0, 256).Select(_ => (float)random.NextDouble()).ToArray();
        var query = new Vector(queryValues, "Query");
        int k = 10;
        
        // Original linear search
        Console.WriteLine("Running original linear search...");
        var sw = Stopwatch.StartNew();
        var originalResults = LinearSearch.Search(vectorList, query, k);
        sw.Stop();
        var originalTime = sw.ElapsedMilliseconds;
        
        // Batch-optimized linear search
        Console.WriteLine("Running batch-optimized linear search...");
        sw.Restart();
        var batchSearch = new BatchOptimizedLinearSearch();
        var batchResults = batchSearch.Search(vectorList, query, k);
        sw.Stop();
        var batchTime = sw.ElapsedMilliseconds;
        
        Console.WriteLine($"Original search time: {originalTime}ms");
        Console.WriteLine($"Batch-optimized search time: {batchTime}ms");
        Console.WriteLine($"Speedup: {(double)originalTime / Math.Max(1, batchTime):F2}x");
        Console.WriteLine($"Results found: {batchResults.Count}");
        Console.WriteLine();
        
        await Task.CompletedTask;
    }

    static async Task PerformanceComparison()
    {
        Console.WriteLine("Example 3: Performance Comparison with Different Dimensions");
        Console.WriteLine("---------------------------------------------------------");
        
        int[] dimensions = { 128, 512, 1024 };
        int vectorCount = 5000;
        
        foreach (int dim in dimensions)
        {
            Console.WriteLine($"\nDimension: {dim}");
            
            // Create test data
            var random = new Random(42);
            var vectors = new List<Vector>();
            for (int i = 0; i < vectorCount; i++)
            {
                float[] values = new float[dim];
                for (int j = 0; j < dim; j++)
                {
                    values[j] = (float)random.NextDouble();
                }
                vectors.Add(new Vector(values));
            }
            
            float[] queryValues = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                queryValues[i] = (float)random.NextDouble();
            }
            var query = new Vector(queryValues);
            
            // Warm up
            _ = query.Distance(vectors[0]);
            _ = query.BatchDistance(vectors.Take(10).ToList());
            
            // Test 1: Sequential distance calculation
            var sw = Stopwatch.StartNew();
            var calculator = new EuclideanDistanceCalculator();
            for (int i = 0; i < vectors.Count; i++)
            {
                _ = calculator.CalculateDistance(query, vectors[i]);
            }
            sw.Stop();
            var sequentialTime = sw.ElapsedMilliseconds;
            
            // Test 2: Batch distance calculation
            sw.Restart();
            _ = query.BatchDistance(vectors);
            sw.Stop();
            var batchTime = sw.ElapsedMilliseconds;
            
            // Test 3: Parallel batch distance calculation
            sw.Restart();
            _ = query.ParallelBatchDistance(vectors);
            sw.Stop();
            var parallelTime = sw.ElapsedMilliseconds;
            
            // Test 4: Combined with cache optimization
            sw.Restart();
            _ = query.CacheOptimizedBatchDistance(vectors);
            sw.Stop();
            var cacheOptimizedTime = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"  Sequential: {sequentialTime}ms");
            Console.WriteLine($"  Batch: {batchTime}ms ({(double)sequentialTime / Math.Max(1, batchTime):F2}x speedup)");
            Console.WriteLine($"  Parallel Batch: {parallelTime}ms ({(double)sequentialTime / Math.Max(1, parallelTime):F2}x speedup)");
            Console.WriteLine($"  Cache-Optimized Batch: {cacheOptimizedTime}ms ({(double)sequentialTime / Math.Max(1, cacheOptimizedTime):F2}x speedup)");
        }
        
        Console.WriteLine("\n");
        
        // Memory usage comparison
        long beforeGC = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long afterGC = GC.GetTotalMemory(true);
        
        Console.WriteLine("Memory usage:");
        Console.WriteLine($"- Before GC: {beforeGC / 1024 / 1024:F2} MB");
        Console.WriteLine($"- After GC:  {afterGC / 1024 / 1024:F2} MB");
        
        await Task.CompletedTask;
    }
}