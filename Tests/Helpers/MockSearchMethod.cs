namespace Neighborly.Tests.Helpers;

using Neighborly;

internal sealed class MockSearchMethod : ISearchMethod
{
    public IList<Vector> Search(IList<Vector> vectors, Vector query, int k) => throw new MockException();
}
