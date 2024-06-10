using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Search
{
    public class SearchService
    {
        private Search.KDTree _kdTree;
        private Search.BallTree _ballTree; 
        private VectorList _vectors;

        public SearchService(VectorList vectors)
        {
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
            if (_vectors == null)
            {
                throw new ArgumentNullException(nameof(_vectors), "Vector list cannot be null");
            }
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
                    return new List<Vector>();  // Other SearchMethods do not support search
            }
        }



    }
}
