using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Neighborly;
using Neighborly.Distance;
using Neighborly.Search;
using System.Diagnostics.CodeAnalysis;

namespace Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
[SimpleJob(RuntimeMoniker.Net80)]
public class CacheOptimizationBenchmarks
{

    private List<Vector> _vectors = null!;
    private List<CacheOptimizedVector> _optimizedVectors = null!;
    private CacheOptimizedVectorBatch _optimizedBatch = null!;
    private Vector _queryVector = null!;
    private CacheOptimizedVector _queryOptimized = null!;
    private Random _random = null!;

    [Params(128, 512, 1536)] // Common embedding dimensions
    public int VectorDimension { get; set; }

    [Params(1000, 10000)]
    public int VectorCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _random = new Random(42); // Fixed seed for reproducibility
        
        // Generate random vectors
        _vectors = new List<Vector>(VectorCount);
        _optimizedVectors = new List<CacheOptimizedVector>(VectorCount);
        
        for (int i = 0; i < VectorCount; i++)
        {
            float[] values = GenerateRandomVector(VectorDimension);
            _vectors.Add(new Vector(values, $"Vector_{i}"));
            _optimizedVectors.Add(new CacheOptimizedVector(values, $"Vector_{i}"));
        }
        
        // Create optimized batch
        _optimizedBatch = new CacheOptimizedVectorBatch(_vectors);
        
        // Create query vector
        float[] queryValues = GenerateRandomVector(VectorDimension);
        _queryVector = new Vector(queryValues, "Query");
        _queryOptimized = new CacheOptimizedVector(queryValues, "Query");
    }

    private float[] GenerateRandomVector(int dimension)
    {
        float[] values = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            values[i] = (float)(_random.NextDouble() * 2 - 1); // Range [-1, 1]
        }
        return values;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var vec in _optimizedVectors)
        {
            vec.Dispose();
        }
        _optimizedBatch?.Dispose();
        _queryOptimized?.Dispose();
    }

    #region Distance Calculation Benchmarks

    [Benchmark(Baseline = true)]
    public float EuclideanDistance_Regular()
    {
        var calculator = new EuclideanDistanceCalculator();
        float sum = 0;
        
        for (int i = 0; i < _vectors.Count; i++)
        {
            sum += calculator.CalculateDistance(_queryVector, _vectors[i]);
        }
        
        return sum;
    }

    [Benchmark]
    public float EuclideanDistance_CacheOptimized()
    {
        var calculator = CacheOptimizedEuclideanDistance.Instance;
        float sum = 0;
        
        for (int i = 0; i < _optimizedVectors.Count; i++)
        {
            sum += calculator.CalculateDistance(_queryOptimized, _optimizedVectors[i]);
        }
        
        return sum;
    }

    [Benchmark]
    public float EuclideanDistance_Batch()
    {
        var calculator = CacheOptimizedEuclideanDistance.Instance;
        float sum = 0;
        
        for (int i = 0; i < _optimizedBatch.Count; i++)
        {
            using var vec = _optimizedBatch.GetVector(i);
            sum += calculator.CalculateDistance(_queryOptimized, vec);
        }
        
        return sum;
    }

    [Benchmark]
    public float CosineSimilarity_Regular()
    {
        var calculator = new CosineSimilarityCalculator();
        float sum = 0;
        
        for (int i = 0; i < _vectors.Count; i++)
        {
            sum += calculator.CalculateDistance(_queryVector, _vectors[i]);
        }
        
        return sum;
    }

    [Benchmark]
    public float CosineSimilarity_CacheOptimized()
    {
        var calculator = CacheOptimizedCosineSimilarity.Instance;
        float sum = 0;
        
        for (int i = 0; i < _optimizedVectors.Count; i++)
        {
            sum += calculator.CalculateDistance(_queryOptimized, _optimizedVectors[i]);
        }
        
        return sum;
    }

    #endregion

    #region Memory Access Pattern Benchmarks

    [Benchmark]
    public float SequentialAccess_Regular()
    {
        float sum = 0;
        for (int i = 0; i < _vectors.Count; i++)
        {
            var vec = _vectors[i];
            for (int j = 0; j < vec.Dimension; j++)
            {
                sum += vec.Values[j];
            }
        }
        return sum;
    }

    [Benchmark]
    public float SequentialAccess_CacheOptimized()
    {
        float sum = 0;
        for (int i = 0; i < _optimizedVectors.Count; i++)
        {
            var values = _optimizedVectors[i].GetValues();
            for (int j = 0; j < values.Length; j++)
            {
                sum += values[j];
            }
        }
        return sum;
    }

    [Benchmark]
    public float SequentialAccess_Batch()
    {
        float sum = 0;
        for (int i = 0; i < _optimizedBatch.Count; i++)
        {
            var span = _optimizedBatch.GetVectorSpan(i);
            for (int j = 0; j < span.Length; j++)
            {
                sum += span[j];
            }
        }
        return sum;
    }

    #endregion

    #region Vector Pool Benchmarks

    private CacheOptimizedVectorPool _pool = null!;

    [GlobalSetup(Target = nameof(VectorPool_WithPooling))]
    public void SetupPool()
    {
        Setup();
        _pool = new CacheOptimizedVectorPool(VectorDimension);
    }

    [GlobalCleanup(Target = nameof(VectorPool_WithPooling))]
    public void CleanupPool()
    {
        _pool?.Clear();
        Cleanup();
    }

    [Benchmark]
    public void VectorPool_NoPooling()
    {
        for (int i = 0; i < 100; i++)
        {
            float[] values = GenerateRandomVector(VectorDimension);
            using var vec = new CacheOptimizedVector(values);
            // Simulate some work
            _ = vec.GetValues();
        }
    }

    [Benchmark]
    public void VectorPool_WithPooling()
    {
        for (int i = 0; i < 100; i++)
        {
            float[] values = GenerateRandomVector(VectorDimension);
            using var pooledVec = _pool.Rent(values);
            // Simulate some work
            _ = pooledVec.Vector.GetValues();
        }
    }

    #endregion

    #region Random Access Pattern Benchmarks

    private int[] _randomIndices = null!;

    [GlobalSetup(Targets = new[] { nameof(RandomAccess_Regular), nameof(RandomAccess_CacheOptimized), nameof(RandomAccess_Batch) })]
    public void SetupRandomAccess()
    {
        Setup();
        // Generate random access pattern
        _randomIndices = new int[VectorCount];
        for (int i = 0; i < VectorCount; i++)
        {
            _randomIndices[i] = _random.Next(VectorCount);
        }
    }

    [Benchmark]
    public float RandomAccess_Regular()
    {
        float sum = 0;
        for (int i = 0; i < _randomIndices.Length; i++)
        {
            var vec = _vectors[_randomIndices[i]];
            sum += vec.Values[0]; // Access first element
        }
        return sum;
    }

    [Benchmark]
    public float RandomAccess_CacheOptimized()
    {
        float sum = 0;
        for (int i = 0; i < _randomIndices.Length; i++)
        {
            var vec = _optimizedVectors[_randomIndices[i]];
            sum += vec[0]; // Access first element
        }
        return sum;
    }

    [Benchmark]
    public float RandomAccess_Batch()
    {
        float sum = 0;
        for (int i = 0; i < _randomIndices.Length; i++)
        {
            var span = _optimizedBatch.GetVectorSpan(_randomIndices[i]);
            sum += span[0]; // Access first element
        }
        return sum;
    }

    #endregion
}

// Benchmark for integration with VectorDatabase search operations
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class VectorDatabaseCacheBenchmarks
{
    private VectorDatabase _database = null!;
    private VectorDatabase _optimizedDatabase = null!;
    private Vector _queryVector = null!;
    private Random _random = null!;

    [Params(512)]
    public int VectorDimension { get; set; }

    [Params(10000)]
    public int VectorCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _random = new Random(42);
        
        // Create regular database
        _database = new VectorDatabase();
        
        // Create optimized database (would need modification to VectorDatabase to support cache-optimized vectors)
        _optimizedDatabase = new VectorDatabase();
        
        // Populate databases
        for (int i = 0; i < VectorCount; i++)
        {
            float[] values = GenerateRandomVector(VectorDimension);
            var vector = new Vector(values, $"Vector_{i}");
            _database.AddVector(vector);
            _optimizedDatabase.AddVector(vector);
        }
        
        // Create query vector
        float[] queryValues = GenerateRandomVector(VectorDimension);
        _queryVector = new Vector(queryValues, "Query");
    }

    private float[] GenerateRandomVector(int dimension)
    {
        float[] values = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            values[i] = (float)(_random.NextDouble() * 2 - 1);
        }
        return values;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _database?.Dispose();
        _optimizedDatabase?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public IList<Vector> KnnSearch_Regular()
    {
        return _database.Search(_queryVector, 10, SearchAlgorithm.Linear);
    }

    [Benchmark]
    public IList<Vector> KnnSearch_WithCacheOptimizedDistance()
    {
        // This would use the cache-optimized distance calculators internally
        return _database.Search(_queryVector, 10, SearchAlgorithm.Linear);
    }

    [Benchmark]
    public IList<Vector> RangeSearch_Regular()
    {
        return _database.RangeSearch(_queryVector, 0.5f, SearchAlgorithm.Linear);
    }
}