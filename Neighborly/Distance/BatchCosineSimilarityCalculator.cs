using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// Batch-optimized cosine similarity calculator with query magnitude caching.
/// </summary>
public sealed class BatchCosineSimilarityCalculator : OptimizedBatchDistanceCalculator
{
    /// <summary>
    /// Singleton instance for reuse.
    /// </summary>
    public static readonly BatchCosineSimilarityCalculator Instance = new();

    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;
        
        for (int i = 0; i < vector1.Dimension; i++)
        {
            dotProduct += vector1.Values[i] * vector2.Values[i];
            magnitudeA += vector1.Values[i] * vector1.Values[i];
            magnitudeB += vector2.Values[i] * vector2.Values[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);
        
        return dotProduct / (magnitudeA * magnitudeB);
    }

    public override void CalculateDistances(Vector query, IList<Vector> candidates, Span<float> results)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(candidates);
        
        if (results.Length < candidates.Count)
            throw new ArgumentException($"Results span must have at least {candidates.Count} elements", nameof(results));

        if (SupportsBatchOptimization && candidates.Count >= 4)
        {
            CalculateDistancesOptimized(query, candidates, results);
        }
        else
        {
            // For small batches, use the optimized version with pre-computed query magnitude
            float queryMagnitude = CalculateQueryMagnitude(query);
            for (int i = 0; i < candidates.Count; i++)
            {
                results[i] = CalculateDistanceWithQueryMagnitude(query, candidates[i], queryMagnitude);
            }
        }
    }

    private void CalculateDistancesOptimized(Vector query, IList<Vector> candidates, Span<float> results)
    {
        // Pre-compute query magnitude once
        float queryMagnitude = CalculateQueryMagnitude(query);
        
        // Convert to cache-optimized format
        using var queryOpt = CacheOptimizedVector.FromVector(query);
        var calculator = CacheOptimizedCosineSimilarity.Instance;
        
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateQueryMagnitude(Vector query)
    {
        float magnitude = 0;
        for (int i = 0; i < query.Dimension; i++)
        {
            magnitude += query.Values[i] * query.Values[i];
        }
        return MathF.Sqrt(magnitude);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CalculateDistanceWithQueryMagnitude(Vector query, Vector candidate, float queryMagnitude)
    {
        float dotProduct = 0;
        float candidateMagnitude = 0;
        
        for (int i = 0; i < query.Dimension; i++)
        {
            dotProduct += query.Values[i] * candidate.Values[i];
            candidateMagnitude += candidate.Values[i] * candidate.Values[i];
        }
        
        candidateMagnitude = MathF.Sqrt(candidateMagnitude);
        return dotProduct / (queryMagnitude * candidateMagnitude);
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
            // Use optimized version with pre-computed query magnitude
            float queryMagnitude = CalculateQueryMagnitude(query);
            for (int i = 0; i < count; i++)
            {
                results[i] = CalculateDistanceWithQueryMagnitude(query, candidates[startIndex + i], queryMagnitude);
            }
        }
    }
}