using System.Text;
using Neighborly.Search;

namespace Neighborly.Tests;

[TestFixture]
public class KDTreeTests
{
    [Test]
    public void CanSaveAndLoad()
    {
        // Arrange
        KDTree originalTree = new();
        VectorList vectors = [new Vector([1f, 2, 3]), new Vector([4f, 5, 6]), new Vector([7f, 8, 9])];
        originalTree.Build(vectors);

        // Act
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            originalTree.Save(writer, vectors);
        }

        stream.Seek(0, SeekOrigin.Begin);
        KDTree loadedTree = new();
        using (var reader = new BinaryReader(stream))
        {
            loadedTree.Load(reader, vectors);
        }

        // Assert
        Assert.That(loadedTree, Is.EqualTo(originalTree));
    }

}
