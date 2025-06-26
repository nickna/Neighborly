using System;
using System.Collections.Generic;
using System.Linq;
using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Enhanced search service that automatically uses batch-optimized algorithms when available.
/// </summary>
public class BatchOptimizedSearchService : SearchService
{
    private readonly bool _useBatchOptimization;
    private readonly bool _enableParallelProcessing;
    private readonly IDistanceCalculator _defaultDistanceCalculator;

    public BatchOptimizedSearchService(VectorList vectors, bool useBatchOptimization = true, bool enableParallelProcessing = false) 
        : base(vectors)
    {
        _useBatchOptimization = useBatchOptimization;
        _enableParallelProcessing = enableParallelProcessing;
        _defaultDistanceCalculator = useBatchOptimization 
            ? BatchEuclideanDistanceCalculator.Instance 
            : new EuclideanDistanceCalculator();
    }

    /// <summary>
    /// Overrides the base search method to use batch-optimized algorithms when appropriate.
    /// </summary>
    public override IList<Vector> Search(Vector query, int k, SearchAlgorithm method = SearchAlgorithm.Linear, float similarityThreshold = 0.5f)
    {
        if (!_useBatchOptimization)
        {
            return base.Search(query, k, method, similarityThreshold);
        }

        IList<Vector> results;

        switch (method)
        {
            case SearchAlgorithm.Linear:
                // Use batch-optimized linear search
                var batchLinearSearch = new BatchOptimizedLinearSearch(_defaultDistanceCalculator);
                results = _enableParallelProcessing 
                    ? batchLinearSearch.ParallelSearch(_vectors, query, k)
                    : batchLinearSearch.Search(_vectors, query, k);
                break;

            case SearchAlgorithm.LSH:
                // LSH can benefit from batch distance calculations in the verification phase
                results = BatchOptimizedLSHSearch(_vectors, query, k);
                break;

            case SearchAlgorithm.BinaryQuantization:
                // Binary quantization can benefit from batch distance calculations
                results = BatchOptimizedBinaryQuantizationSearch(_vectors, query, k);
                break;

            default:
                // For other algorithms, use the base implementation
                return base.Search(query, k, method, similarityThreshold);
        }

        // Apply similarity threshold filtering
        return ApplySimilarityThreshold(results, query, similarityThreshold);
    }

    /// <summary>
    /// Overrides the base range search method to use batch-optimized algorithms.
    /// </summary>
    public override IList<Vector> RangeSearch(Vector query, float radius, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
    {
        if (!_useBatchOptimization)
        {
            return base.RangeSearch(query, radius, method, distanceCalculator);
        }

        distanceCalculator ??= _defaultDistanceCalculator;

        switch (method)
        {
            case SearchAlgorithm.Linear:
            case SearchAlgorithm.Range:
                // Use batch-optimized linear range search
                return _enableParallelProcessing
                    ? BatchOptimizedLinearRangeSearch.ParallelSearch(_vectors, query, radius, distanceCalculator)
                    : BatchOptimizedLinearRangeSearch.Search(_vectors, query, radius, distanceCalculator);

            default:
                // For other algorithms, use the base implementation
                return base.RangeSearch(query, radius, method, distanceCalculator);
        }
    }

    private IList<Vector> BatchOptimizedLSHSearch(VectorList vectors, Vector query, int k)
    {
        // Get candidates from LSH buckets
        var candidates = LSHSearch.GetCandidates(vectors, query);
        
        if (candidates.Count == 0)
        {
            return new List<Vector>();
        }

        // Use batch distance calculation for verification
        float[] distances;
        if (_defaultDistanceCalculator is IBatchDistanceCalculator batchCalc)
        {
            distances = batchCalc.CalculateDistances(query, candidates);
        }
        else
        {
            distances = query.BatchDistance(candidates, _defaultDistanceCalculator);
        }

        // Sort and return top k
        return GetTopK(candidates, distances, k);
    }

    private IList<Vector> BatchOptimizedBinaryQuantizationSearch(VectorList vectors, Vector query, int k)
    {
        // Get candidates from binary quantization
        var candidates = BinaryQuantization.GetCandidates(vectors, query);
        
        if (candidates.Count == 0)
        {
            return new List<Vector>();
        }

        // Use batch distance calculation for final ranking
        float[] distances;
        if (_defaultDistanceCalculator is IBatchDistanceCalculator batchCalc)
        {
            distances = batchCalc.CalculateDistances(query, candidates);
        }
        else
        {
            distances = query.BatchDistance(candidates, _defaultDistanceCalculator);
        }

        // Sort and return top k
        return GetTopK(candidates, distances, k);
    }

    private IList<Vector> GetTopK(IList<Vector> candidates, float[] distances, int k)
    {
        var indexedDistances = new (int index, float distance)[candidates.Count];
        for (int i = 0; i < distances.Length; i++)
        {
            indexedDistances[i] = (i, distances[i]);
        }

        return indexedDistances
            .OrderBy(x => x.distance)
            .Take(k)
            .Select(x => candidates[x.index])
            .ToList();
    }

    private IList<Vector> ApplySimilarityThreshold(IList<Vector> results, Vector query, float similarityThreshold)
    {
        // Intelligent threshold application logic from base class
        bool isHighDimensional = query.Values.Length > 50;
        bool hasLargeDistances = results.Count > 0 && results.Any(v => v.Distance(query) > 5.0f);
        
        if (isHighDimensional && hasLargeDistances && similarityThreshold > 1.5f)
        {
            return results;
        }
        
        return results.Where(v => v.Distance(query) <= similarityThreshold).ToList();
    }
}