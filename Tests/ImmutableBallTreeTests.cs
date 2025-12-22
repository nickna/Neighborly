using Neighborly;
using Neighborly.Search;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class ImmutableBallTreeTests
{
    private VectorList _vectors = null!;

    [SetUp]
    public void Setup()
    {
        _vectors = new VectorList();

        // Create test vectors
        for (int i = 0; i < 100; i++)
        {
            var values = new float[] { i, i * 2, i * 3 };
            _vectors.Add(new Vector(values, $"Vector {i}"));
        }
    }

    [TearDown]
    public void TearDown()
    {
        _vectors?.Dispose();
    }

    [Test]
    public async Task BuildAsync_CreatesValidTree()
    {
        var tree = await ImmutableBallTree.BuildAsync(_vectors);

        Assert.That(tree.Root, Is.Not.Null);
    }

    [Test]
    public async Task BuildAsync_EmptyVectorList_ReturnsEmptyTree()
    {
        using var emptyVectors = new VectorList();
        var tree = await ImmutableBallTree.BuildAsync(emptyVectors);

        Assert.That(tree.Root, Is.Null);
    }

    [Test]
    public async Task Search_FindsCorrectNeighbors()
    {
        var tree = await ImmutableBallTree.BuildAsync(_vectors);

        // Query with a vector close to Vector 50
        var query = new Vector(new float[] { 50, 100, 150 });
        var results = tree.Search(query, 5);

        Assert.That(results, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task Search_EmptyTree_ReturnsEmpty()
    {
        var tree = new ImmutableBallTree();

        var query = new Vector(new float[] { 1, 2, 3 });
        var results = tree.Search(query, 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Search_KGreaterThanVectors_ReturnsAllVectors()
    {
        using var smallVectors = new VectorList();
        for (int i = 0; i < 3; i++)
        {
            smallVectors.Add(new Vector(new float[] { i, i, i }));
        }

        var tree = await ImmutableBallTree.BuildAsync(smallVectors);
        var query = new Vector(new float[] { 0, 0, 0 });
        var results = tree.Search(query, 10);

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ConcurrentReads_AreThreadSafe()
    {
        var tree = await ImmutableBallTree.BuildAsync(_vectors);

        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        for (int i = 0; i < 100; i++)
        {
            var queryIndex = i % _vectors.Count;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var query = new Vector(new float[] { queryIndex, queryIndex * 2, queryIndex * 3 });
                    var results = tree.Search(query, 5);
                    Assert.That(results, Has.Count.EqualTo(5));
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.That(exceptions, Is.Empty, () => $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
    }

    [Test]
    public void Search_ThrowsOnNullQuery()
    {
        var tree = new ImmutableBallTree();

        Assert.Throws<ArgumentNullException>(() => tree.Search(null!, 5));
    }

    [Test]
    public void Search_ThrowsOnInvalidK()
    {
        var tree = new ImmutableBallTree();

        var query = new Vector(new float[] { 1, 2, 3 });
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Search(query, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Search(query, -1));
    }

    [Test]
    public async Task ImmutableBallTreeNode_IsLeaf_CorrectForLeafNodes()
    {
        using var smallVectors = new VectorList();
        smallVectors.Add(new Vector(new float[] { 1, 2, 3 }));

        var tree = await ImmutableBallTree.BuildAsync(smallVectors);

        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root!.IsLeaf, Is.True);
    }

    [Test]
    public async Task ImmutableBallTreeNode_IsLeaf_CorrectForInternalNodes()
    {
        var tree = await ImmutableBallTree.BuildAsync(_vectors);

        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root!.IsLeaf, Is.False);
    }

    [Test]
    public async Task ImmutableBallTreeNode_HasValidRadiusAndCenter()
    {
        var tree = await ImmutableBallTree.BuildAsync(_vectors);

        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root!.Center, Is.Not.Null);
        Assert.That(tree.Root!.Radius, Is.GreaterThanOrEqualTo(0));
    }
}
