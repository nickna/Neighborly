using System;
using System.Collections.Generic;
using System.Linq;

namespace Neighborly
{
    /// <summary>
    /// Ball tree search method. 
    /// A ball tree is a binary tree that recursively divides the space into balls.
    /// </summary>
    public class BallTreeSearch : ISearchMethod
    {
        public IList<Vector> Search(IList<Vector> vectors, Vector query, int k)
        {
            // Create a BallTree from the vectors
            var ballTree = new BallTree(vectors);

            // Perform a k-nearest neighbors search
            return ballTree.Search(query, k);
        }
    }

}
