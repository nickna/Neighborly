namespace Neighborly
{
    using System.Collections.Generic;
    public interface ISearchMethod
    {
        IList<Vector> Search(IList<Vector> vectors, Vector query, int k);
    }
}