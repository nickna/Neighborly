namespace Neighborly.Tests.Helpers;

using Neighborly;
using Neighborly.Search;

internal sealed class MockSearchService : SearchService
{
    public MockSearchService(VectorList vectors) : base(vectors) { }
    public IList<Vector> Search(Vector query, int k) => throw new MockException();
}
