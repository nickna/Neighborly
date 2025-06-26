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
        public void BuildAllIndexes()
        {
            // TODO -- Examine memory footprint and performance of each index
            BuildIndex(SearchAlgorithm.KDTree);
            BuildIndex(SearchAlgorithm.BallTree);
            BuildIndex(SearchAlgorithm.HNSW);
        }

        public void Clear()
        {
            _kdTree = new();
            _ballTree = new();
            _hnsw = new();
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
    }
}
