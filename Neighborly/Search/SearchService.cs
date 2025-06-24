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

        private readonly VectorList _vectors;
        private Search.KDTree _kdTree;
        private Search.BallTree _ballTree;
        private Search.HNSW _hnsw;
        private Search.LSHSearch? _lshSearch; // Nullable until configured/built
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
            // _lshSearch is initialized when BuildIndex(SearchAlgorithm.LSH) is called,
            // as it requires vector dimensions which might only be known at build time.
            embeddingGenerator = EmbeddingGenerator.Instance;
        }

        /// <summary>
        /// Build all indexes for the given vector list
        /// (Not recommended for production use)
        /// </summary>
        public void BuildAllIndexes()
        {
            // TODO -- Examine memory footprint and performance of each index
            BuildIndex(SearchAlgorithm.KDTree);
            BuildIndex(SearchAlgorithm.BallTree);
            BuildIndex(SearchAlgorithm.HNSW);
            BuildIndex(SearchAlgorithm.LSH); // Add LSH to BuildAllIndexes
        }

        public void Clear()
        {
            _kdTree = new();
            _ballTree = new();
            _hnsw = new();
            _lshSearch = null; // Clear LSH search instance
        }

        public void BuildIndex(SearchAlgorithm method)
        {
            if (_vectors.Count == 0)
            {
                return;
            }

            switch (method)
            {
                case SearchAlgorithm.KDTree:
                    _kdTree.Build(_vectors);
                    break;
                case SearchAlgorithm.BallTree:
                    _ballTree.Build(_vectors);
                    break;
                case SearchAlgorithm.HNSW:
                    _hnsw.Build(_vectors);
                    break;
                case SearchAlgorithm.LSH:
                    if (_vectors.Count > 0)
                    {
                        // Ensure LSHSearch is initialized with correct dimensions
                        // This assumes all vectors have the same dimension.
                        // A more robust approach might get dimensions from VectorList metadata if available
                        // or from the first vector if the list is not empty.
                        int dimensions = _vectors[0].Values.Length;
                        var lshConfig = new LSHConfig(vectorDimensions: dimensions); // Use default L, k for now
                        _lshSearch = new LSHSearch(lshConfig);
                        _lshSearch.Build(_vectors);
                    }
                    break;
                default:
                    return;  // Other SearchMethods do not require building an index
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

        public IList<Vector> Search(string text, int k, SearchAlgorithm method = SearchAlgorithm.KDTree, float? similarityThreshold = null)
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
        public IList<Vector> Search(Vector query, int k, SearchAlgorithm method = SearchAlgorithm.KDTree, float similarityThreshold = 0.5f)
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
                    if (_lshSearch == null)
                    {
                        // Attempt to build it on-the-fly if not already built.
                        // This might be slow for the first LSH search if the index is large.
                        // Consider if this is the desired behavior or if an explicit BuildIndex is required.
                        BuildIndex(SearchAlgorithm.LSH);
                    }
                    if (_lshSearch != null)
                    {
                        results = _lshSearch.Search(query, k);
                    }
                    else
                    {
                        // Fallback or error if LSH couldn't be initialized (e.g., no vectors to get dimensions)
                        results = new List<Vector>(); // Or throw new InvalidOperationException("LSH index not built and cannot be initialized.");
                    }
                    break;
                case SearchAlgorithm.HNSW:
                    results = _hnsw.Search(query, k);
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
        public IList<Vector> RangeSearch(string text, float radius, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
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
        public IList<Vector> RangeSearch(Vector query, float radius, SearchAlgorithm method = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
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
                    case SearchAlgorithm.LSH:
                        // Assuming _vectors is already populated or LSH Load will handle it.
                        // LSH Load needs a way to get dimensions if not part of the saved data.
                        // For now, let's assume LSHConfig is part of the saved data or can be inferred.
                        if (_vectors.Count > 0) // Need dimensions to initialize LSHConfig if not saved
                        {
                            int dims = _vectors[0].Values.Length; // Infer dimensions
                             // This is a simplified Load. A proper Load would read config from stream.
                            var lshConfig = new LSHConfig(vectorDimensions: dims);
                            _lshSearch = new LSHSearch(lshConfig);
                            // _lshSearch.Load(reader, dims); // Placeholder: Actual load logic needed in LSHSearch
                        }
                        // If LSHSearch.Load can fully rehydrate itself including config, this would be cleaner.
                        // For now, LSH load is not fully implemented, so this path might not work.
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported search method for LoadAsync: {method}");
                }
            }
        }

        public async Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.Write(s_currentFileVersion);

            var builtIndexes = new List<SearchAlgorithm>();
            // TODO: Add IsBuilt properties to KDTree and BallTree for reliable checks.
            // For now, we assume if they are not null, they might be built.
            // This part needs more robust checking of whether an index is actually built and worth saving.
            if (_kdTree != null) builtIndexes.Add(SearchAlgorithm.KDTree); // Placeholder check
            if (_ballTree != null) builtIndexes.Add(SearchAlgorithm.BallTree); // Placeholder check
            if (_hnsw != null && _hnsw.Count > 0) builtIndexes.Add(SearchAlgorithm.HNSW);
            if (_lshSearch != null) builtIndexes.Add(SearchAlgorithm.LSH); // LSHSearch needs an IsBuilt or Count property

            // Filter out LSH if its Save is not implemented, to avoid runtime errors
            // builtIndexes.RemoveAll(sa => sa == SearchAlgorithm.LSH && _lshSearch?.IsSaveImplemented == false); // Needs IsSaveImplemented property

            writer.Write(builtIndexes.Count);

            foreach (var method in builtIndexes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExportIndexAsync(writer, method, cancellationToken).ConfigureAwait(false);
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
                case SearchAlgorithm.LSH:
                    _lshSearch?.Save(writer); // Call LSH Save (currently a placeholder)
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported search method for ExportIndexAsync: {method}");
            }
        }
    }
}
