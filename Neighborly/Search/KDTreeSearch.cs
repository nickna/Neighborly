namespace Neighborly
{
    /// <summary>
    /// k-d tree search method. (https://en.wikipedia.org/wiki/K-d_tree)
    /// </summary>
    public class KDTreeSearch : ISearchMethod
    {
        private KDTree _kdTree;

        public KDTreeSearch()
        {
            _kdTree = new KDTree();
        }

        public IList<Vector> Search(IList<Vector> vectors, Vector query, int k)
        {
            // Build the k-d tree with the vectors
            _kdTree.Build(vectors);

            // Perform the nearest neighbor search
            var results = _kdTree.NearestNeighbors(query, k);

            return results;
        }
    }
}
