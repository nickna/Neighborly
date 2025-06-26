using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// Abstract base class for distance calculators that provides default batch processing implementation.
/// </summary>
public abstract class AbstractBatchDistanceCalculator : AbstractDistanceCalculator, IBatchDistanceCalculator
{
    /// <inheritdoc/>
    public virtual bool SupportsBatchOptimization => false;

    /// <inheritdoc/>
    public virtual int GetOptimalBatchSize(int dimension)
    {
        // Default batch size based on typical L1/L2 cache sizes
        // Assumes ~32KB L1 cache, with each vector taking dimension * 4 bytes
        // We want to fit the query vector + several candidates in L1
        int bytesPerVector = dimension * sizeof(float);
        int l1CacheSize = 32 * 1024; // 32KB typical L1 data cache
        int maxVectorsInL1 = l1CacheSize / bytesPerVector;
        
        // Leave room for query vector and other data
        return Math.Max(1, Math.Min(maxVectorsInL1 - 2, 64));
    }

    /// <inheritdoc/>
    public virtual void CalculateDistances(Vector query, IList<Vector> candidates, Span<float> results)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        
        if (results.Length < candidates.Count)
            throw new ArgumentException($"Results span must have at least {candidates.Count} elements", nameof(results));

        // Default implementation: calculate distances sequentially
        for (int i = 0; i < candidates.Count; i++)
        {
            results[i] = CalculateDistance(query, candidates[i]);
        }
    }

    /// <inheritdoc/>
    public virtual float[] CalculateDistances(Vector query, IList<Vector> candidates)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);

        float[] results = new float[candidates.Count];
        CalculateDistances(query, candidates, results.AsSpan());
        return results;
    }

    /// <inheritdoc/>
    public virtual void CalculateDistancesRange(Vector query, IList<Vector> candidates, int startIndex, int count, Span<float> results)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        
        if (startIndex < 0 || startIndex >= candidates.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        
        if (count < 0 || startIndex + count > candidates.Count)
            throw new ArgumentOutOfRangeException(nameof(count));
        
        if (results.Length < count)
            throw new ArgumentException($"Results span must have at least {count} elements", nameof(results));

        // Default implementation: calculate distances sequentially
        for (int i = 0; i < count; i++)
        {
            results[i] = CalculateDistance(query, candidates[startIndex + i]);
        }
    }
}

/// <summary>
/// Base class for batch distance calculators with cache-friendly optimizations.
/// </summary>
public abstract class OptimizedBatchDistanceCalculator : AbstractBatchDistanceCalculator
{
    /// <inheritdoc/>
    public override bool SupportsBatchOptimization => true;

    /// <summary>
    /// Performs optimized batch distance calculations using cache-friendly memory access patterns.
    /// </summary>
    protected void ProcessBatchOptimized(Vector query, IList<Vector> candidates, Span<float> results, 
                                       Func<float[], ReadOnlySpan<float[]>, Span<float>, int, int, bool> simdProcessor)
    {
        int dimension = query.Dimension;
        int batchSize = GetOptimalBatchSize(dimension);
        
        // Process in cache-friendly batches
        for (int start = 0; start < candidates.Count; start += batchSize)
        {
            int end = Math.Min(start + batchSize, candidates.Count);
            int count = end - start;
            
            // Try SIMD processing first
            bool processed = false;
            if (count >= 4) // Minimum for SIMD efficiency
            {
                // Prepare batch data for SIMD processing
                float[][] batchData = new float[count][];
                for (int i = 0; i < count; i++)
                {
                    batchData[i] = candidates[start + i].Values;
                }
                
                processed = simdProcessor(query.Values, batchData, results.Slice(start, count), 0, count);
            }
            
            // Fallback to scalar processing if SIMD not available or failed
            if (!processed)
            {
                for (int i = 0; i < count; i++)
                {
                    results[start + i] = CalculateDistance(query, candidates[start + i]);
                }
            }
        }
    }

    /// <summary>
    /// Pre-computes query-specific values that can be reused across all distance calculations.
    /// </summary>
    protected virtual void PrecomputeQueryData(Vector query, out object? precomputedData)
    {
        precomputedData = null;
    }
}