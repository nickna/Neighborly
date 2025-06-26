using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Neighborly.Distance;

namespace Neighborly;

/// <summary>
/// Object pool for cache-optimized vectors to reduce allocation overhead.
/// </summary>
public sealed class CacheOptimizedVectorPool
{
    private readonly ConcurrentBag<PooledVector> _pool = new();
    private readonly int _maxPoolSize;
    private readonly int _dimension;
    private int _currentPoolSize;
    
    /// <summary>
    /// Default shared pool for common dimensions.
    /// </summary>
    private static readonly ConcurrentDictionary<int, CacheOptimizedVectorPool> s_sharedPools = new();
    
    public CacheOptimizedVectorPool(int dimension, int maxPoolSize = 1000)
    {
        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));
        if (maxPoolSize <= 0)
            throw new ArgumentException("Max pool size must be positive", nameof(maxPoolSize));
            
        _dimension = dimension;
        _maxPoolSize = maxPoolSize;
    }
    
    /// <summary>
    /// Gets a shared pool for the specified dimension.
    /// </summary>
    public static CacheOptimizedVectorPool GetSharedPool(int dimension)
    {
        return s_sharedPools.GetOrAdd(dimension, d => new CacheOptimizedVectorPool(d));
    }
    
    /// <summary>
    /// Rents a vector from the pool or creates a new one if pool is empty.
    /// </summary>
    public PooledVector Rent(float[] values, string? originalText = null, short[]? tags = null)
    {
        if (values.Length != _dimension)
            throw new ArgumentException($"Values must have dimension {_dimension}", nameof(values));
            
        PooledVector? pooled = null;
        
        if (_pool.TryTake(out pooled))
        {
            Interlocked.Decrement(ref _currentPoolSize);
            pooled.Initialize(values, originalText, tags);
            return pooled;
        }
        
        // Create new if pool is empty
        return new PooledVector(this, values, originalText, tags);
    }
    
    /// <summary>
    /// Returns a vector to the pool.
    /// </summary>
    internal void Return(PooledVector vector)
    {
        if (vector == null)
            return;
            
        // Only return to pool if we haven't exceeded max size
        if (Interlocked.Increment(ref _currentPoolSize) <= _maxPoolSize)
        {
            vector.Clear();
            _pool.Add(vector);
        }
        else
        {
            // Pool is full, dispose the vector
            Interlocked.Decrement(ref _currentPoolSize);
            vector.DisposeInternal();
        }
    }
    
    /// <summary>
    /// Clears the pool and disposes all pooled vectors.
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out var vector))
        {
            vector.DisposeInternal();
            Interlocked.Decrement(ref _currentPoolSize);
        }
    }
    
    public int Dimension => _dimension;
    public int CurrentPoolSize => _currentPoolSize;
    public int MaxPoolSize => _maxPoolSize;
}

/// <summary>
/// A pooled cache-optimized vector that returns to the pool when disposed.
/// </summary>
public sealed class PooledVector : IDisposable
{
    private readonly CacheOptimizedVectorPool _pool;
    private CacheOptimizedVector? _vector;
    private bool _isDisposed;
    
    internal PooledVector(CacheOptimizedVectorPool pool, float[] values, 
                         string? originalText, short[]? tags)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _vector = new CacheOptimizedVector(values, originalText, tags);
    }
    
    internal void Initialize(float[] values, string? originalText, short[]? tags)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(PooledVector));
            
        // Dispose old vector if exists
        _vector?.Dispose();
        
        // Create new vector with provided values
        _vector = new CacheOptimizedVector(values, originalText, tags);
        _isDisposed = false;
    }
    
    internal void Clear()
    {
        // Reset metadata but keep allocated memory
        if (_vector != null)
        {
            // We'll recreate the vector on next use, but this clears references
            // to strings and arrays to help GC
            _vector.Dispose();
            _vector = null;
        }
        // Reset disposal flag so it can be reused
        _isDisposed = false;
    }
    
    public CacheOptimizedVector Vector
    {
        get
        {
            if (_isDisposed || _vector == null)
                throw new ObjectDisposedException(nameof(PooledVector));
            return _vector;
        }
    }
    
    public bool IsDisposed => _isDisposed;
    
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _pool.Return(this);
        }
    }
    
    internal void DisposeInternal()
    {
        _isDisposed = true;
        _vector?.Dispose();
        _vector = null;
    }
}

/// <summary>
/// Extension methods for working with cache-optimized vectors.
/// </summary>
public static class CacheOptimizedVectorExtensions
{
    /// <summary>
    /// Converts a collection of regular vectors to a cache-optimized batch.
    /// </summary>
    public static CacheOptimizedVectorBatch ToCacheOptimizedBatch(this IList<Vector> vectors)
    {
        return new CacheOptimizedVectorBatch(vectors);
    }
    
    /// <summary>
    /// Performs a cache-optimized distance calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CacheOptimizedDistance(this Vector vector1, Vector vector2, 
                                               IDistanceCalculator? calculator = null)
    {
        calculator ??= CacheOptimizedEuclideanDistance.Instance;
        
        if (calculator is CacheOptimizedEuclideanDistance euclidean)
        {
            using var opt1 = CacheOptimizedVector.FromVector(vector1);
            using var opt2 = CacheOptimizedVector.FromVector(vector2);
            return euclidean.CalculateDistance(opt1, opt2);
        }
        else if (calculator is CacheOptimizedCosineSimilarity cosine)
        {
            using var opt1 = CacheOptimizedVector.FromVector(vector1);
            using var opt2 = CacheOptimizedVector.FromVector(vector2);
            return cosine.CalculateDistance(opt1, opt2);
        }
        else
        {
            // Fall back to regular calculation
            return calculator.CalculateDistance(vector1, vector2);
        }
    }
    
    /// <summary>
    /// Performs batch distance calculations with cache optimization.
    /// </summary>
    public static float[] CacheOptimizedBatchDistance(this Vector query, IList<Vector> vectors,
                                                     IDistanceCalculator? calculator = null)
    {
        calculator ??= CacheOptimizedEuclideanDistance.Instance;
        
        using var queryOpt = CacheOptimizedVector.FromVector(query);
        using var batch = new CacheOptimizedVectorBatch(vectors);
        
        var results = new float[vectors.Count];
        
        if (calculator is CacheOptimizedEuclideanDistance euclidean)
        {
            for (int i = 0; i < vectors.Count; i++)
            {
                using var targetOpt = batch.GetVector(i);
                results[i] = euclidean.CalculateDistance(queryOpt, targetOpt);
            }
        }
        else if (calculator is CacheOptimizedCosineSimilarity cosine)
        {
            for (int i = 0; i < vectors.Count; i++)
            {
                using var targetOpt = batch.GetVector(i);
                results[i] = cosine.CalculateDistance(queryOpt, targetOpt);
            }
        }
        else
        {
            // Fall back to regular calculation
            for (int i = 0; i < vectors.Count; i++)
            {
                results[i] = calculator.CalculateDistance(query, vectors[i]);
            }
        }
        
        return results;
    }
}