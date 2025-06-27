using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neighborly.Distance;

namespace Neighborly.Search
{
    public class SearchService
    {
        /// <summary>
        /// The version of the database file format that this class writes.
        /// </summary>
        private const int s_currentFileVersion = 1;
        private const int s_numberOfStorableIndexes = 3;

        protected readonly VectorList _vectors;
        private Search.KDTree _kdTree;
        private Search.BallTree _ballTree;
        private Search.HNSW _hnsw;
        private EmbeddingGenerator embeddingGenerator = EmbeddingGenerator.Instance;
        public EmbeddingGenerator EmbeddingGenerator
        {
            get => embeddingGenerator;
            set => embeddingGenerator = value;
        }

        public SearchService(VectorList vectors)
        {
            ArgumentNullException.ThrowIfNull(vectors);

            _vectors = vectors;
            _kdTree = new();
            _ballTree = new();
            _hnsw = new();
            embeddingGenerator = EmbeddingGenerator.Instance;
        }

        /// <summary>
        /// Build all indexes for the given vector list
        /// (Not recommended for production use)
        /// </summary>
        public async Task BuildAllIndexes(CancellationToken cancellationToken = default)
        {
            // TODO -- Examine memory footprint and performance of each index
            await BuildIndexes(SearchAlgorithm.KDTree, cancellationToken).ConfigureAwait(false);
            await BuildIndexes(SearchAlgorithm.BallTree, cancellationToken).ConfigureAwait(false);
            await BuildIndexes(SearchAlgorithm.HNSW, cancellationToken).ConfigureAwait(false);
        }

        public void Clear()
        {
            _kdTree = new();
            _ballTree = new();
            _hnsw = new();
        }

        internal Task BuildIndexes(SearchAlgorithm method, CancellationToken cancellationToken = default)
    {
        if (_vectors.Count == 0)
        {
            return Task.CompletedTask;
        }

        switch (method)
        {
            case SearchAlgorithm.KDTree:
                return _kdTree.Build(_vectors);
            case SearchAlgorithm.BallTree:
                _ballTree.Build(_vectors);
                return Task.CompletedTask;
            case SearchAlgorithm.HNSW:
                return _hnsw.BuildAsync(_vectors, cancellationToken);
            default:
                return Task.CompletedTask;  // Other SearchMethods do not require building an index
        }
    }

        private float CalculateDefaultThreshold(string text)
        {
            // Adjust these values based on your specific requirements
            const float FullTextThreshold = 0.5f;
            const float PartialTextThreshold = 0.8f;
            const int FullTextLengthThreshold = 20; // Consider text shorter than this as partial
            const int PartialTextLengthThreshold = 5; // Very short queries should have an even lower threshold

            if (text.Length < PartialTextLengthThreshold)
            {
                return 0.9f; // Even more lenient for very short queries
            }
            else if (text.Length < FullTextLengthThreshold)
            {
                return PartialTextThreshold;
            }
            else
            {
                return FullTextThreshold;
            }
        }

        public virtual IList<Vector> Search(string text, int k, SearchAlgorithm method = SearchAlgorithm.KDTree, float? similarityThreshold = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text), "Text cannot be null or empty");
            }

            // If no threshold is provided, calculate a default based on the text length
            float effectiveThreshold = similarityThreshold ?? CalculateDefaultThreshold(text);

            // Convert text into an embedding
            var embedding = embeddingGenerator.GenerateEmbedding(text);            
            var query = new Vector(embedding);
            var results = this.Search(query, k, method, effectiveThreshold);


            // For partial text searches, also consider prefix matching
            if (text.Length < 20) // Adjust this threshold as needed
            {
                var prefixMatches = _vectors.Where(v => v.OriginalText.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                                            .Take(k)
                                            .ToList();
                results = results.Concat(prefixMatches).Distinct().Take(k).ToList();
            }

            return results;

        }
        public virtual IList<Vector> Search(Vector query, int k, SearchAlgorithm method = SearchAlgorithm.KDTree, float similarityThreshold = 0.5f)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
            }
            if (k <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
            }

            IList<Vector> results;
            switch (method)
            {
                case SearchAlgorithm.KDTree:
                    results = _kdTree.NearestNeighbors(query, k);
                    break;
                case SearchAlgorithm.BallTree:
                    results = _ballTree.Search(query, k);
                    break;
                case SearchAlgorithm.Linear:
                    results = LinearSearch.Search(_vectors, query, k);
                    break;
                case SearchAlgorithm.LSH:
                    results = LSHSearch.Search(_vectors, query, k);
                    break;
                case SearchAlgorithm.HNSW:
                    results = _hnsw.Search(query, k);
                    break;
                case SearchAlgorithm.BinaryQuantization:
                    results = BinaryQuantization.Search(_vectors, query, k);
                    break;
                case SearchAlgorithm.ProductQuantization:
                    results = ProductQuantization.Search(_vectors, query, k);
                    break;
                default:
                    return [];  // Other SearchMethods do not support search
            }

            // Apply similarity threshold intelligently based on results
            // For high-dimensional vectors (likely text embeddings) with large distances, be more lenient
            // Text embeddings typically have dimensions > 100 and distances > 5.0
            bool isHighDimensional = query.Values.Length > 50;
            bool hasLargeDistances = results.Count > 0 && results.Any(v => v.Distance(query) > 5.0f);
            
            // Only bypass threshold for clearly inappropriate large thresholds or when using specific algorithms
            // that are known to work better with algorithm-native results
            if (isHighDimensional && hasLargeDistances && similarityThreshold > 1.5f)
            {
                // For moderate to large thresholds with text embeddings and large distances,
                // trust the algorithm's results rather than applying strict distance filtering
                return results;
            }
            
            // For other cases, apply threshold filtering
            var filteredResults = results.Where(v => v.Distance(query) <= similarityThreshold).ToList();
            
            return filteredResults;
        }

        /// <summary>
        /// Performs range search to find all vectors within a specified radius of the text query.
        /// The text is first converted into an embedding using the EmbeddingGenerator.
        /// </summary>
        /// <param name="text">The text to search for</param>
        /// <param name="radius">The maximum distance from the query</param>
        /// <param name="method">The search algorithm to use</param>
        /// <param name="distanceCalculator">The distance calculator to use (defaults to Euclidean)</param>
        /// <returns>A list of vectors within the specified radius, ordered by distance</returns>
        public virtual IList<Vector> RangeSearch(string text, float radius, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text), "Text cannot be null or empty");
            }

            // Convert text into an embedding
            var embedding = embeddingGenerator.GenerateEmbedding(text);
            var query = new Vector(embedding);
            
            return RangeSearch(query, radius, method, distanceCalculator);
        }

        /// <summary>
        /// Performs range search to find all vectors within a specified radius of the query vector.
        /// </summary>
        /// <param name="query">The query vector</param>
        /// <param name="radius">The maximum distance from the query</param>
        /// <param name="method">The search algorithm to use</param>
        /// <param name="distanceCalculator">The distance calculator to use (defaults to Euclidean)</param>
        /// <returns>A list of vectors within the specified radius, ordered by distance</returns>
        public virtual IList<Vector> RangeSearch(Vector query, float radius, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
            }
            if (radius <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
            }

            distanceCalculator ??= EuclideanDistanceCalculator.Instance;

            IList<Vector> results;
            switch (method)
            {
                case SearchAlgorithm.Linear:
                case SearchAlgorithm.Range:
                    results = LinearRangeSearch.Search(_vectors, query, radius, distanceCalculator);
                    break;
                case SearchAlgorithm.KDTree:
                    results = _kdTree.RangeNeighbors(query, radius, distanceCalculator);
                    break;
                default:
                    throw new NotSupportedException($"Range search is not yet supported for {method} algorithm");
            }

            return results;
        }

        public async Task LoadAsync(BinaryReader reader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var version = reader.ReadInt32(); // Read the version number
            if (version != s_currentFileVersion)
            {
                throw new InvalidDataException($"Unsupported file version {version}");
            }

            var count = reader.ReadInt32(); // Read the number of index
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var method = (SearchAlgorithm)reader.ReadInt32();
                switch (method)
                {
                    case SearchAlgorithm.KDTree:
                        _kdTree.Load(reader, _vectors);
                        break;
                    case SearchAlgorithm.BallTree:
                        await _ballTree.LoadAsync(reader, _vectors, cancellationToken).ConfigureAwait(false);
                        break;
                    case SearchAlgorithm.HNSW:
                        await _hnsw.LoadAsync(reader, _vectors, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported search method");
                }
            }
        }

        public async Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.Write(s_currentFileVersion);

            // Count how many indexes we actually have built
            int actualIndexCount = 0;
            // For now, be conservative and only save indexes we know have data
            // KDTree and BallTree don't expose public state properties, so we can't reliably check if they're built
            bool hasKDTree = false; // TODO: Add IsBuilt property to KDTree
            bool hasBallTree = false; // TODO: Add IsBuilt property to BallTree  
            bool hasHNSW = _hnsw != null && _hnsw.Count > 0; // HNSW is built if it has nodes
            
            if (hasKDTree) actualIndexCount++;
            if (hasBallTree) actualIndexCount++;
            if (hasHNSW) actualIndexCount++;

            writer.Write(actualIndexCount); // Write the actual number of indexes

            if (hasKDTree)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExportIndexAsync(writer, SearchAlgorithm.KDTree, cancellationToken).ConfigureAwait(false);
            }

            if (hasBallTree)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExportIndexAsync(writer, SearchAlgorithm.BallTree, cancellationToken).ConfigureAwait(false);
            }

            if (hasHNSW)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExportIndexAsync(writer, SearchAlgorithm.HNSW, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ExportIndexAsync(BinaryWriter writer, SearchAlgorithm method, CancellationToken cancellationToken)
        {
            writer.Write((int)method);

            switch (method)
            {
                case SearchAlgorithm.KDTree:
                    _kdTree.Save(writer, _vectors);
                    break;
                case SearchAlgorithm.BallTree:
                    await _ballTree.SaveAsync(writer, cancellationToken).ConfigureAwait(false);
                    break;
                case SearchAlgorithm.HNSW:
                    await _hnsw.SaveAsync(writer, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported search method");
            }
        }

        #region Metadata Filtering Search Methods

        /// <summary>
        /// Performs text search with metadata filtering support.
        /// </summary>
        /// <param name="text">The text to search for</param>
        /// <param name="k">Number of nearest neighbors to return</param>
        /// <param name="metadataFilter">Metadata filter to apply</param>
        /// <param name="method">The search algorithm to use</param>
        /// <param name="similarityThreshold">Similarity threshold for filtering results</param>
        /// <returns>A list of vectors matching the criteria, ordered by distance</returns>
        public virtual IList<Vector> SearchWithMetadata(string text, int k, MetadataFilter? metadataFilter, SearchAlgorithm method = SearchAlgorithm.KDTree, float? similarityThreshold = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text), "Text cannot be null or empty");
            }

            // Convert text into an embedding
            var embedding = embeddingGenerator.GenerateEmbedding(text);
            var query = new Vector(embedding);
            
            return SearchWithMetadata(query, k, metadataFilter, method, similarityThreshold ?? CalculateDefaultThreshold(text));
        }

        /// <summary>
        /// Performs vector search with metadata filtering support.
        /// </summary>
        /// <param name="query">The query vector</param>
        /// <param name="k">Number of nearest neighbors to return</param>
        /// <param name="metadataFilter">Metadata filter to apply</param>
        /// <param name="method">The search algorithm to use</param>
        /// <param name="similarityThreshold">Similarity threshold for filtering results</param>
        /// <returns>A list of vectors matching the criteria, ordered by distance</returns>
        public virtual IList<Vector> SearchWithMetadata(Vector query, int k, MetadataFilter? metadataFilter, SearchAlgorithm method = SearchAlgorithm.KDTree, float similarityThreshold = 0.5f)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
            }
            if (k <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
            }

            // If no metadata filter is provided, use the original search method
            if (metadataFilter == null || !metadataFilter.HasFilters)
            {
                return Search(query, k, method, similarityThreshold);
            }

            // Create filter predicate
            var filterPredicate = MetadataFilterEvaluator.CreatePredicate(metadataFilter);

            // Apply filtering strategy based on algorithm
            return SearchWithMetadataFilter(query, k, method, similarityThreshold, filterPredicate);
        }

        /// <summary>
        /// Performs range search with metadata filtering support.
        /// </summary>
        /// <param name="text">The text to search for</param>
        /// <param name="radius">The maximum distance from the query</param>
        /// <param name="metadataFilter">Metadata filter to apply</param>
        /// <param name="method">The search algorithm to use</param>
        /// <param name="distanceCalculator">The distance calculator to use</param>
        /// <returns>A list of vectors within the specified radius and matching metadata criteria</returns>
        public virtual IList<Vector> RangeSearchWithMetadata(string text, float radius, MetadataFilter? metadataFilter, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text), "Text cannot be null or empty");
            }

            // Convert text into an embedding
            var embedding = embeddingGenerator.GenerateEmbedding(text);
            var query = new Vector(embedding);
            
            return RangeSearchWithMetadata(query, radius, metadataFilter, method, distanceCalculator);
        }

        /// <summary>
        /// Performs range search with metadata filtering support.
        /// </summary>
        /// <param name="query">The query vector</param>
        /// <param name="radius">The maximum distance from the query</param>
        /// <param name="metadataFilter">Metadata filter to apply</param>
        /// <param name="method">The search algorithm to use</param>
        /// <param name="distanceCalculator">The distance calculator to use</param>
        /// <returns>A list of vectors within the specified radius and matching metadata criteria</returns>
        public virtual IList<Vector> RangeSearchWithMetadata(Vector query, float radius, MetadataFilter? metadataFilter, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
            }
            if (radius <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than 0");
            }

            // If no metadata filter is provided, use the original range search method
            if (metadataFilter == null || !metadataFilter.HasFilters)
            {
                return RangeSearch(query, radius, method, distanceCalculator);
            }

            // Create filter predicate
            var filterPredicate = MetadataFilterEvaluator.CreatePredicate(metadataFilter);

            // Apply filtering strategy for range search
            return RangeSearchWithMetadataFilter(query, radius, method, distanceCalculator, filterPredicate);
        }

        #endregion

        #region Private Metadata Filtering Implementation

        /// <summary>
        /// Performs search with metadata filtering using the optimal strategy for the given algorithm.
        /// </summary>
        private IList<Vector> SearchWithMetadataFilter(Vector query, int k, SearchAlgorithm method, float similarityThreshold, Func<Vector, bool> filterPredicate)
        {
            switch (method)
            {
                case SearchAlgorithm.Linear:
                    // For linear search, apply filter during iteration for optimal performance
                    return LinearSearchWithFilter(query, k, filterPredicate);

                case SearchAlgorithm.KDTree:
                case SearchAlgorithm.BallTree:
                    // For tree-based algorithms, we need to potentially expand search to find enough filtered results
                    return TreeSearchWithFilter(query, k, method, similarityThreshold, filterPredicate);

                case SearchAlgorithm.LSH:
                case SearchAlgorithm.HNSW:
                case SearchAlgorithm.BinaryQuantization:
                case SearchAlgorithm.ProductQuantization:
                    // For approximate algorithms, use post-filtering with expanded search
                    return ApproximateSearchWithFilter(query, k, method, similarityThreshold, filterPredicate);

                default:
                    throw new NotSupportedException($"Metadata filtering is not supported for algorithm: {method}");
            }
        }

        /// <summary>
        /// Performs range search with metadata filtering.
        /// </summary>
        private IList<Vector> RangeSearchWithMetadataFilter(Vector query, float radius, SearchAlgorithm method, IDistanceCalculator? distanceCalculator, Func<Vector, bool> filterPredicate)
        {
            // For range search, we typically need to filter during the search process
            distanceCalculator ??= new EuclideanDistanceCalculator();

            var candidates = new List<Vector>();
            
            // Apply filter during candidate collection
            foreach (var vector in _vectors)
            {
                if (!filterPredicate(vector))
                    continue;

                var distance = distanceCalculator.CalculateDistance(query, vector);
                if (distance <= radius)
                {
                    candidates.Add(vector);
                }
            }

            // Sort by distance
            return candidates.OrderBy(v => distanceCalculator.CalculateDistance(query, v)).ToList();
        }

        /// <summary>
        /// Linear search with integrated metadata filtering.
        /// </summary>
        private IList<Vector> LinearSearchWithFilter(Vector query, int k, Func<Vector, bool> filterPredicate)
        {
            var candidates = new List<(Vector vector, float distance)>();

            foreach (var vector in _vectors)
            {
                if (!filterPredicate(vector))
                    continue;

                var distance = vector.Distance(query);
                candidates.Add((vector, distance));
            }

            return candidates
                .OrderBy(c => c.distance)
                .Take(k)
                .Select(c => c.vector)
                .ToList();
        }

        /// <summary>
        /// Tree-based search with metadata filtering using dynamic expansion and selectivity optimization.
        /// For tree algorithms, we may need to search beyond k to find enough filtered results.
        /// </summary>
        private IList<Vector> TreeSearchWithFilter(Vector query, int k, SearchAlgorithm method, float similarityThreshold, Func<Vector, bool> filterPredicate)
        {
            // Estimate filter selectivity to optimize search strategy
            var selectivity = EstimateFilterSelectivity(filterPredicate, Math.Min(1000, _vectors.Count));
            
            // Dynamic expansion based on estimated selectivity
            int expandedK = CalculateOptimalExpansion(k, selectivity, _vectors.Count);
            
            // Special handling for very low selectivity - use linear search directly
            if (selectivity < 0.01 && _vectors.Count > 5000)
            {
                return LinearSearchWithFilter(query, k, filterPredicate);
            }
            
            var candidates = Search(query, expandedK, method, similarityThreshold);
            var filteredResults = candidates.Where(filterPredicate).Take(k).ToList();

            // Progressive expansion if we need more results
            int maxAttempts = 3;
            int attempt = 1;
            
            while (filteredResults.Count < k && expandedK < _vectors.Count && attempt < maxAttempts)
            {
                // Increase expansion factor progressively
                int newExpandedK = Math.Min(_vectors.Count, expandedK * 2);
                if (newExpandedK == expandedK) break; // No more expansion possible
                
                candidates = Search(query, newExpandedK, method, similarityThreshold);
                filteredResults = candidates.Where(filterPredicate).Take(k).ToList();
                
                expandedK = newExpandedK;
                attempt++;
            }

            // Final fallback to linear search if still insufficient results
            if (filteredResults.Count < k / 2) // Only if we have very few results
            {
                return LinearSearchWithFilter(query, k, filterPredicate);
            }

            return filteredResults;
        }

        /// <summary>
        /// Approximate search algorithms with metadata filtering using dynamic expansion.
        /// </summary>
        private IList<Vector> ApproximateSearchWithFilter(Vector query, int k, SearchAlgorithm method, float similarityThreshold, Func<Vector, bool> filterPredicate)
        {
            // Estimate filter selectivity for approximate algorithms
            var selectivity = EstimateFilterSelectivity(filterPredicate, Math.Min(500, _vectors.Count));
            
            // Conservative expansion for approximate algorithms (they're already approximate)
            int baseExpansion = (int)(CalculateOptimalExpansion(k, selectivity, _vectors.Count) * 0.7);
            int expandedK = Math.Max(k * 2, Math.Min(baseExpansion, _vectors.Count));
            
            var candidates = Search(query, expandedK, method, similarityThreshold);
            var filteredResults = candidates.Where(filterPredicate).Take(k).ToList();

            // For approximate algorithms, be more aggressive about fallback due to quality trade-offs
            if (filteredResults.Count < k * 0.6) // If we have less than 60% of what we need
            {
                // Try one more expansion before falling back to linear
                if (expandedK < _vectors.Count)
                {
                    int secondExpansion = Math.Min(_vectors.Count, expandedK * 2);
                    candidates = Search(query, secondExpansion, method, similarityThreshold);
                    filteredResults = candidates.Where(filterPredicate).Take(k).ToList();
                }
                
                // Final fallback to linear search if still insufficient
                if (filteredResults.Count < k * 0.4)
                {
                    return LinearSearchWithFilter(query, k, filterPredicate);
                }
            }

            return filteredResults;
        }

        /// <summary>
        /// Estimates the selectivity of a metadata filter by sampling a subset of vectors.
        /// </summary>
        /// <param name="filterPredicate">The filter predicate to test</param>
        /// <param name="sampleSize">Number of vectors to sample for estimation</param>
        /// <returns>Estimated selectivity as a ratio between 0 and 1</returns>
        private double EstimateFilterSelectivity(Func<Vector, bool> filterPredicate, int sampleSize)
        {
            if (_vectors.Count == 0) return 0.0;
            
            sampleSize = Math.Min(sampleSize, _vectors.Count);
            int matches = 0;
            
            // Use systematic sampling for better representation
            int step = Math.Max(1, _vectors.Count / sampleSize);
            int sampledCount = 0;
            
            for (int i = 0; i < _vectors.Count && sampledCount < sampleSize; i += step)
            {
                if (filterPredicate(_vectors[i]))
                {
                    matches++;
                }
                sampledCount++;
            }
            
            return sampledCount > 0 ? (double)matches / sampledCount : 0.0;
        }

        /// <summary>
        /// Calculates the optimal expansion factor based on filter selectivity.
        /// </summary>
        /// <param name="k">Requested number of results</param>
        /// <param name="selectivity">Estimated filter selectivity (0-1)</param>
        /// <param name="totalVectors">Total number of vectors in the collection</param>
        /// <returns>Optimal number of candidates to search</returns>
        private int CalculateOptimalExpansion(int k, double selectivity, int totalVectors)
        {
            if (selectivity <= 0.0) return totalVectors; // No matches expected, search all
            
            // Calculate expansion factor based on selectivity with safety margin
            double safetyMargin = 1.5; // 50% safety margin
            double requiredExpansion = (k / selectivity) * safetyMargin;
            
            // Apply reasonable bounds
            int minExpansion = k * 2;     // At least 2x expansion
            int maxExpansion = k * 20;    // At most 20x expansion
            
            int expansion = (int)Math.Ceiling(Math.Max(minExpansion, Math.Min(maxExpansion, requiredExpansion)));
            
            return Math.Min(expansion, totalVectors);
        }

        #endregion
    }
}
