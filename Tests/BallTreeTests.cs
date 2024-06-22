using System.Text;
using Neighborly.Search;

namespace Neighborly.Tests;

[TestFixture]
public class BallTreeTests
{
    [Test]
    public async Task CanSaveAndLoad()
    {
        // Arrange
        BallTree originalTree = new();
        VectorList vectors = [new Vector([1f, 2, 3]), new Vector([4f, 5, 6]), new Vector([7f, 8, 9])];
        originalTree.Build(vectors);

        // Act
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            await originalTree.SaveAsync(writer, vectors).ConfigureAwait(true);
        }

        stream.Seek(0, SeekOrigin.Begin);
        BallTree loadedTree = new();
        using (var reader = new BinaryReader(stream))
        {
            await loadedTree.LoadAsync(reader, vectors).ConfigureAwait(true);
        }

        // Assert
        Assert.That(loadedTree, Is.EqualTo(originalTree));
    }
}
