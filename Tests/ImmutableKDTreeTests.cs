using Neighborly;
using Neighborly.Search;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class ImmutableKDTreeTests
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
        var tree = await ImmutableKDTree.BuildAsync(_vectors);

        Assert.That(tree.Root, Is.Not.Null);
    }

    [Test]
    public async Task BuildAsync_EmptyVectorList_ReturnsEmptyTree()
    {
        using var emptyVectors = new VectorList();
        var tree = await ImmutableKDTree.BuildAsync(emptyVectors);

        Assert.That(tree.Root, Is.Null);
    }

    [Test]
    public async Task NearestNeighbors_FindsCorrectNeighbors()
    {
        var tree = await ImmutableKDTree.BuildAsync(_vectors);

        // Query with a vector close to Vector 50
        var query = new Vector(new float[] { 50, 100, 150 });
        var results = tree.NearestNeighbors(query, 5);

        Assert.That(results, Has.Count.EqualTo(5));

        // The closest should be near Vector 50 (within a reasonable range)
        var closest = results[0];
        // Extract the number from "Vector N" and check it's close to 50
        var numberStr = closest.OriginalText.Replace("Vector ", "");
        var number = int.Parse(numberStr);
        Assert.That(number, Is.InRange(45, 55), $"Expected closest vector to be near 50, but got {closest.OriginalText}");
    }

    [Test]
    public async Task NearestNeighbors_EmptyTree_ReturnsEmpty()
    {
        var tree = new ImmutableKDTree();

        var query = new Vector(new float[] { 1, 2, 3 });
        var results = tree.NearestNeighbors(query, 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task NearestNeighbors_KGreaterThanVectors_ReturnsAllVectors()
    {
        using var smallVectors = new VectorList();
        for (int i = 0; i < 3; i++)
        {
            smallVectors.Add(new Vector(new float[] { i, i, i }));
        }

        var tree = await ImmutableKDTree.BuildAsync(smallVectors);
        var query = new Vector(new float[] { 0, 0, 0 });
        var results = tree.NearestNeighbors(query, 10);

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task RangeNeighbors_FindsVectorsWithinRadius()
    {
        var tree = await ImmutableKDTree.BuildAsync(_vectors);

        // Query with a vector at Vector 50 position
        var query = new Vector(new float[] { 50, 100, 150 });
        var results = tree.RangeNeighbors(query, 10.0f);

        Assert.That(results, Is.Not.Empty);

        // All results should be within radius 10
        foreach (var result in results)
        {
            var distance = query.Distance(result);
            Assert.That(distance, Is.LessThanOrEqualTo(10.0f));
        }
    }

    [Test]
    public async Task RangeNeighbors_EmptyTree_ReturnsEmpty()
    {
        var tree = new ImmutableKDTree();

        var query = new Vector(new float[] { 1, 2, 3 });
        var results = tree.RangeNeighbors(query, 100.0f);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task RangeNeighbors_SmallRadius_ReturnsLimitedResults()
    {
        var tree = await ImmutableKDTree.BuildAsync(_vectors);

        var query = new Vector(new float[] { 50, 100, 150 });
        var results = tree.RangeNeighbors(query, 0.1f);

        // Very small radius might return 0 or 1 results
        Assert.That(results.Count, Is.LessThanOrEqualTo(1));
    }

    [Test]
    public async Task ConcurrentReads_AreThreadSafe()
    {
        var tree = await ImmutableKDTree.BuildAsync(_vectors);

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
                    var results = tree.NearestNeighbors(query, 5);
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
    public void NearestNeighbors_ThrowsOnNullQuery()
    {
        var tree = new ImmutableKDTree();

        Assert.Throws<ArgumentNullException>(() => tree.NearestNeighbors(null!, 5));
    }

    [Test]
    public void NearestNeighbors_ThrowsOnInvalidK()
    {
        var tree = new ImmutableKDTree();

        var query = new Vector(new float[] { 1, 2, 3 });
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.NearestNeighbors(query, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.NearestNeighbors(query, -1));
    }

    [Test]
    public void RangeNeighbors_ThrowsOnNullQuery()
    {
        var tree = new ImmutableKDTree();

        Assert.Throws<ArgumentNullException>(() => tree.RangeNeighbors(null!, 10.0f));
    }

    [Test]
    public void RangeNeighbors_ThrowsOnInvalidRadius()
    {
        var tree = new ImmutableKDTree();

        var query = new Vector(new float[] { 1, 2, 3 });
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeNeighbors(query, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeNeighbors(query, -1));
    }

    [Test]
    public async Task ImmutableKDTreeNode_IsLeaf_CorrectForLeafNodes()
    {
        using var smallVectors = new VectorList();
        smallVectors.Add(new Vector(new float[] { 1, 2, 3 }));

        var tree = await ImmutableKDTree.BuildAsync(smallVectors);

        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root!.IsLeaf, Is.True);
    }

    [Test]
    public async Task ImmutableKDTreeNode_IsLeaf_CorrectForInternalNodes()
    {
        var tree = await ImmutableKDTree.BuildAsync(_vectors);

        Assert.That(tree.Root, Is.Not.Null);
        Assert.That(tree.Root!.IsLeaf, Is.False);
    }
}
