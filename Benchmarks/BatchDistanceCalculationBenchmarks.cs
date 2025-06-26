using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Neighborly;
using Neighborly.Distance;
using Neighborly.Search;

namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[DisassemblyDiagnoser(maxDepth: 3)]
public class BatchDistanceCalculationBenchmarks
{
    private VectorList _vectors = null!;
    private Vector _queryVector = null!;
    private List<Vector> _vectorsList = null!;
    private Random _random = null!;

    [Params(128, 512, 1536)] // Common embedding dimensions
    public int VectorDimension { get; set; }

    [Params(1000, 5000, 10000)]
    public int VectorCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _random = new Random(42); // Fixed seed for reproducibility
        
        // Generate random vectors
        _vectors = new VectorList();
        _vectorsList = new List<Vector>(VectorCount);
        
        for (int i = 0; i < VectorCount; i++)
        {
            float[] values = GenerateRandomVector(VectorDimension);
            var vector = new Vector(values, $"Vector_{i}");
            _vectors.Add(vector);
            _vectorsList.Add(vector);
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
            values[i] = (float)(_random.NextDouble() * 2 - 1); // Range [-1, 1]
        }
        return values;
    }

    #region Single Distance Calculations

    [Benchmark(Baseline = true)]
    public float[] SingleDistance_Euclidean()
    {
        var calculator = new EuclideanDistanceCalculator();
        float[] results = new float[_vectorsList.Count];
        
        for (int i = 0; i < _vectorsList.Count; i++)
        {
            results[i] = calculator.CalculateDistance(_queryVector, _vectorsList[i]);
        }
        
        return results;
    }

    [Benchmark]
    public float[] SingleDistance_Cosine()
    {
        var calculator = new CosineSimilarityCalculator();
        float[] results = new float[_vectorsList.Count];
        
        for (int i = 0; i < _vectorsList.Count; i++)
        {
            results[i] = calculator.CalculateDistance(_queryVector, _vectorsList[i]);
        }
        
        return results;
    }

    #endregion

    #region Batch Distance Calculations

    [Benchmark]
    public float[] BatchDistance_Euclidean()
    {
        var calculator = BatchEuclideanDistanceCalculator.Instance;
        return calculator.CalculateDistances(_queryVector, _vectorsList);
    }

    [Benchmark]
    public float[] BatchDistance_Cosine()
    {
        var calculator = BatchCosineSimilarityCalculator.Instance;
        return calculator.CalculateDistances(_queryVector, _vectorsList);
    }

    [Benchmark]
    public float[] BatchDistance_Extension()
    {
        // Using extension method which automatically uses batch optimization
        return _queryVector.BatchDistance(_vectorsList);
    }

    [Benchmark]
    public float[] BatchDistance_Parallel()
    {
        // Using parallel batch calculation
        return _queryVector.ParallelBatchDistance(_vectorsList);
    }

    #endregion

    #region Search Algorithm Benchmarks

    [Benchmark]
    public IList<Vector> LinearSearch_Original()
    {
        return LinearSearch.Search(_vectors, _queryVector, 10);
    }

    [Benchmark]
    public IList<Vector> LinearSearch_BatchOptimized()
    {
        var search = new BatchOptimizedLinearSearch();
        return search.Search(_vectors, _queryVector, 10);
    }

    [Benchmark]
    public IList<Vector> LinearSearch_BatchParallel()
    {
        var search = new BatchOptimizedLinearSearch();
        return search.ParallelSearch(_vectors, _queryVector, 10);
    }

    [Benchmark]
    public IList<Vector> RangeSearch_Original()
    {
        return LinearRangeSearch.Search(_vectors, _queryVector, 1.0f);
    }

    [Benchmark]
    public IList<Vector> RangeSearch_BatchOptimized()
    {
        return BatchOptimizedLinearRangeSearch.Search(_vectors, _queryVector, 1.0f);
    }

    [Benchmark]
    public IList<Vector> RangeSearch_BatchParallel()
    {
        return BatchOptimizedLinearRangeSearch.ParallelSearch(_vectors, _queryVector, 1.0f);
    }

    #endregion

    #region Cache Efficiency Benchmarks

    [Benchmark]
    public float[] BatchDistance_WithCacheOptimization()
    {
        // This combines batch processing with cache-optimized vectors
        using var queryOpt = CacheOptimizedVector.FromVector(_queryVector);
        using var batch = new CacheOptimizedVectorBatch(_vectorsList);
        
        var calculator = CacheOptimizedEuclideanDistance.Instance;
        float[] results = new float[_vectorsList.Count];
        
        for (int i = 0; i < _vectorsList.Count; i++)
        {
            using var targetOpt = batch.GetVector(i);
            results[i] = calculator.CalculateDistance(queryOpt, targetOpt);
        }
        
        return results;
    }

    #endregion
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class BatchSizeBenchmarks
{
    private List<Vector> _vectors = null!;
    private Vector _queryVector = null!;
    private IBatchDistanceCalculator _calculator = null!;

    [Params(512)]
    public int VectorDimension { get; set; }

    [Params(10000)]
    public int VectorCount { get; set; }

    [Params(16, 32, 64, 128, 256)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _vectors = new List<Vector>(VectorCount);
        
        for (int i = 0; i < VectorCount; i++)
        {
            float[] values = new float[VectorDimension];
            for (int j = 0; j < VectorDimension; j++)
            {
                values[j] = (float)random.NextDouble();
            }
            _vectors.Add(new Vector(values));
        }
        
        float[] queryValues = new float[VectorDimension];
        for (int i = 0; i < VectorDimension; i++)
        {
            queryValues[i] = (float)random.NextDouble();
        }
        _queryVector = new Vector(queryValues);
        
        _calculator = BatchEuclideanDistanceCalculator.Instance;
    }

    [Benchmark]
    public float[] ProcessInBatches()
    {
        float[] results = new float[_vectors.Count];
        
        for (int start = 0; start < _vectors.Count; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize, _vectors.Count);
            var batch = _vectors.GetRange(start, end - start);
            
            var batchResults = _calculator.CalculateDistances(_queryVector, batch);
            batchResults.CopyTo(results, start);
        }
        
        return results;
    }
}