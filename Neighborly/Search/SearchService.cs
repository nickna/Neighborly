using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Search
{
    public class SearchService
    {
        /// <summary>
        /// The version of the database file format that this class writes.
        /// </summary>
        private const int s_currentFileVersion = 1;
        private const int s_numberOfStorableIndexes = 2;
        private const int _defaultLockTimeout = 5000;
        private readonly SemaphoreSlim _loadSaveSemaphore = new(1, 1);
        private readonly SemaphoreSlim _rebuildSemaphore = new(1, 1);

        private readonly VectorList _vectors;
        private Search.KDTree _kdTree;
        private Search.BallTree _ballTree;
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
            embeddingGenerator = EmbeddingGenerator.Instance;
        }

        /// <summary>
        /// Build all indexes for the given vector list
        /// </summary>
        public async Task BuildAllIndexes()
        {
            await Task.Run(() =>
            {
                BuildIndex(SearchAlgorithm.KDTree);
                BuildIndex(SearchAlgorithm.BallTree);
            }).ConfigureAwait(false);
        }

        public void Clear()
        {
            _kdTree = new();
            _ballTree = new();
        }

        public void BuildIndex(SearchAlgorithm method)
        {
            if (_vectors.Count == 0)
            {
                return;
            }
            _rebuildSemaphore.Wait();

            try
            {
                switch (method)
                {
                    case SearchAlgorithm.KDTree:
                        _kdTree.Build(_vectors);
                        break;
                    case SearchAlgorithm.BallTree:
                        _ballTree.Build(_vectors);
                        break;
                    default:
                        return;  // Other SearchMethods do not require building an index
                }
            }
            finally
            {
                _rebuildSemaphore.Release();
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
            
            if (_rebuildSemaphore.Wait(millisecondsTimeout: _defaultLockTimeout))
            {
                IList<Vector> results;
                try
                {
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
                        default:
                            return [];  // Other SearchMethods do not support search
                    }
                }
                finally
                {
                    _rebuildSemaphore.Release();
                }

                // Apply similarity threshold
                return results.Where(v => v.Distance(query) <= similarityThreshold).ToList();
            }
            else
            {
                // Timeout waiting for the rebuild semaphore
                return [];

            }

            
        }

        public async Task LoadAsync(BinaryReader reader, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reader);
            await _loadSaveSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false); // Enforce singleton access to LoadAsync and SaveAsync
            try
            {
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
                        default:
                            throw new InvalidOperationException("Unsupported search method");
                    }
                }
            }
            finally
            {
                _loadSaveSemaphore.Release();
            }
        }

        public async Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            await _loadSaveSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false); // Enforce singleton access to LoadAsync and SaveAsync

            try
            {
                writer.Write(s_currentFileVersion);

                // Count how many indexes we actually have
                int actualIndexCount = 0;
                if (_kdTree != null) actualIndexCount++;
                if (_ballTree != null) actualIndexCount++;

                writer.Write(actualIndexCount); // Write the actual number of indexes

                if (_kdTree != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ExportIndexAsync(writer, SearchAlgorithm.KDTree, cancellationToken).ConfigureAwait(false);
                }

                if (_ballTree != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ExportIndexAsync(writer, SearchAlgorithm.BallTree, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _loadSaveSemaphore.Release();
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
                default:
                    throw new InvalidOperationException("Unsupported search method");
            }
        }
    }
}
