using System;
using System.Collections.Generic;
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

        private readonly VectorList _vectors;
        private Search.KDTree _kdTree;
        private Search.BallTree _ballTree;

        public SearchService(VectorList vectors)
        {
            ArgumentNullException.ThrowIfNull(vectors);

            _vectors = vectors;
            _kdTree = new();
            _ballTree = new();
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

        public IList<Vector> Search(Vector query, int k, SearchAlgorithm method = SearchAlgorithm.KDTree)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query), "Query vector cannot be null");
            }
            if (k <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
            }

            switch (method)
            {
                case SearchAlgorithm.KDTree:
                    return _kdTree.NearestNeighbors(query, k);
                case SearchAlgorithm.BallTree:
                    return _ballTree.Search(query, k);
                case SearchAlgorithm.Linear:
                    return LinearSearch.Search(_vectors, query, k);
                case SearchAlgorithm.LSH:
                    return LSHSearch.Search(_vectors, query, k);
                default:
                    return [];  // Other SearchMethods do not support search
            }
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
                        _ballTree.Load(reader, _vectors);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported search method");
                }
            }
        }

        public Task SaveAsync(BinaryWriter writer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.Write(s_currentFileVersion);
            writer.Write(s_numberOfStorableIndexes); // Write the number of indexes

            cancellationToken.ThrowIfCancellationRequested();
            ExportIndex(writer, SearchAlgorithm.KDTree);

            cancellationToken.ThrowIfCancellationRequested();
            ExportIndex(writer, SearchAlgorithm.BallTree);
            return Task.CompletedTask;
        }

        private void ExportIndex(BinaryWriter writer, SearchAlgorithm method)
        {
            writer.Write((int)method);

            switch (method)
            {
                case SearchAlgorithm.KDTree:
                    _kdTree.Save(writer, _vectors);
                    break;
                case SearchAlgorithm.BallTree:
                    _ballTree.Save(writer, _vectors);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported search method");
            }
        }
    }
}
