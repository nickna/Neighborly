using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Neighborly.Distance;

/// <summary>
/// Batch-optimized Euclidean distance calculator with SIMD support.
/// </summary>
public sealed class BatchEuclideanDistanceCalculator : OptimizedBatchDistanceCalculator
{
    /// <summary>
    /// Singleton instance for reuse.
    /// </summary>
    public static readonly BatchEuclideanDistanceCalculator Instance = new();

    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float sum = 0;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            float diff = vector1.Values[i] - vector2.Values[i];
            sum += diff * diff;
        }
        return MathF.Sqrt(sum);
    }

    public override void CalculateDistances(Vector query, IList<Vector> candidates, Span<float> results)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        
        if (results.Length < candidates.Count)
            throw new ArgumentException($"Results span must have at least {candidates.Count} elements", nameof(results));

        // Use cache-optimized vectors if available
        if (SupportsBatchOptimization && candidates.Count >= 4)
        {
            CalculateDistancesOptimized(query, candidates, results);
        }
        else
        {
            // Fallback to base implementation
            base.CalculateDistances(query, candidates, results);
        }
    }

    private void CalculateDistancesOptimized(Vector query, IList<Vector> candidates, Span<float> results)
    {
        // Convert to cache-optimized format for better performance
        using var queryOpt = CacheOptimizedVector.FromVector(query);
        var calculator = CacheOptimizedEuclideanDistance.Instance;
        
        int batchSize = GetOptimalBatchSize(query.Dimension);
        
        // Process in batches for optimal cache usage
        for (int start = 0; start < candidates.Count; start += batchSize)
        {
            int end = Math.Min(start + batchSize, candidates.Count);
            
            // Create optimized batch for this chunk
            var batchCandidates = new List<Vector>(end - start);
            for (int i = start; i < end; i++)
            {
                batchCandidates.Add(candidates[i]);
            }
            
            using var batch = new CacheOptimizedVectorBatch(batchCandidates);
            
            // Calculate distances for this batch
            for (int i = 0; i < batchCandidates.Count; i++)
            {
                using var targetOpt = batch.GetVector(i);
                results[start + i] = calculator.CalculateDistance(queryOpt, targetOpt);
            }
        }
    }

    public override void CalculateDistancesRange(Vector query, IList<Vector> candidates, int startIndex, int count, Span<float> results)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        
        if (startIndex < 0 || startIndex >= candidates.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        
        if (count < 0 || startIndex + count > candidates.Count)
            throw new ArgumentOutOfRangeException(nameof(count));
        
        if (results.Length < count)
            throw new ArgumentException($"Results span must have at least {count} elements", nameof(results));

        if (SupportsBatchOptimization && count >= 4)
        {
            // Create a sub-list view for the range
            var rangeList = new List<Vector>(count);
            for (int i = 0; i < count; i++)
            {
                rangeList.Add(candidates[startIndex + i]);
            }
            
            CalculateDistancesOptimized(query, rangeList, results);
        }
        else
        {
            // Fallback to base implementation
            base.CalculateDistancesRange(query, candidates, startIndex, count, results);
        }
    }
}

/// <summary>
/// Extension methods for batch distance calculations.
/// </summary>
public static class BatchDistanceExtensions
{
    /// <summary>
    /// Calculates distances between a query vector and multiple candidates using the most efficient method available.
    /// </summary>
    public static float[] BatchDistance(this Vector query, IList<Vector> candidates, IDistanceCalculator? calculator = null)
    {
        calculator ??= BatchEuclideanDistanceCalculator.Instance;
        
        if (calculator is IBatchDistanceCalculator batchCalc)
        {
            return batchCalc.CalculateDistances(query, candidates);
        }
        else
        {
            // Fallback to sequential calculation
            float[] results = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                results[i] = calculator.CalculateDistance(query, candidates[i]);
            }
            return results;
        }
    }

    /// <summary>
    /// Performs parallel batch distance calculations for very large candidate sets.
    /// </summary>
    public static float[] ParallelBatchDistance(this Vector query, IList<Vector> candidates, IDistanceCalculator? calculator = null)
    {
        calculator ??= BatchEuclideanDistanceCalculator.Instance;
        float[] results = new float[candidates.Count];
        
        if (calculator is IBatchDistanceCalculator batchCalc)
        {
            int batchSize = batchCalc.GetOptimalBatchSize(query.Dimension);
            int degreeOfParallelism = Environment.ProcessorCount;
            
            Parallel.For(0, candidates.Count, new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism }, 
                        i =>
            {
                results[i] = calculator.CalculateDistance(query, candidates[i]);
            });
        }
        else
        {
            // Fallback to parallel sequential calculation
            Parallel.For(0, candidates.Count, i =>
            {
                results[i] = calculator.CalculateDistance(query, candidates[i]);
            });
        }
        
        return results;
    }
}