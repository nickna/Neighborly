using Neighborly;

[TestFixture]
public class VectorTests
{
    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor_As_BinaryReader()
    {
        // Arrange
        float[] floatArray = [1.0f, 2.1f, 3.2f, 4.5f, 5.7f];
        Vector originalVector = new(floatArray, "Test");

        // Act
        byte[] binary = originalVector.ToBinary();
        using MemoryStream ms = new(binary);
        using BinaryReader br = new(ms);
        Vector newVector = new(br);
        byte[] newBinary = newVector.ToBinary();

        // Assert
        Assert.That(newVector.Id, Is.EqualTo(originalVector.Id));
        Assert.That(newVector.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(newVector.Values, Is.EqualTo(originalVector.Values));
        Assert.That(newVector.Tags, Is.EqualTo(originalVector.Tags));
        Assert.That(newBinary, Is.EqualTo(binary));
    }

    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor_As_Array()
    {
        // Arrange
        float[] floatArray = [1.0f, 2.1f, 3.2f, 4.5f, 5.7f];
        Vector originalVector = new(floatArray, "Test");

        // Act
        byte[] binary = originalVector.ToBinary();
        Vector newVector = new(binary);
        byte[] newBinary = newVector.ToBinary();

        // Assert
        Assert.That(newVector.Id, Is.EqualTo(originalVector.Id));
        Assert.That(newVector.OriginalText, Is.EqualTo(originalVector.OriginalText));
        Assert.That(newVector.Values, Is.EqualTo(originalVector.Values));
        Assert.That(newVector.Tags, Is.EqualTo(originalVector.Tags));
        Assert.That(newBinary, Is.EqualTo(binary));
    }
}
