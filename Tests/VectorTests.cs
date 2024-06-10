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

    public void InPlaceAdd()
    {
        // Arrange
        Vector firstAddend = new([1.0f, 2.0f]);
        Vector secondAddend = new([3.1f, 4.2f]);

        // Act
        firstAddend.InPlaceAdd(secondAddend);

        // Assert
        Assert.That(firstAddend.Values[0], Is.EqualTo(4.1f).Within(0.01f));
        Assert.That(firstAddend.Values[1], Is.EqualTo(6.2f).Within(0.01f));
    }

    [Test]
    public void InPlaceSubtract()
    {
        // Arrange
        Vector minuend = new([1.0f, 2.0f]);
        Vector subtrahend = new([3.1f, 4.2f]);

        // Act
        minuend.InPlaceSubtract(subtrahend);

        // Assert
        Assert.That(minuend.Values[0], Is.EqualTo(-2.1f).Within(0.01f));
        Assert.That(minuend.Values[1], Is.EqualTo(-2.2f).Within(0.01f));
    }

    [Test]
    public void InPlaceDivideByInt()
    {
        // Arrange
        Vector divisor = new([25.0f, 100.0f]);
        int scalar = 5;

        // Act
        divisor.InPlaceDivide(scalar);

        // Assert
        Assert.That(divisor.Values[0], Is.EqualTo(5f).Within(0.01f));
        Assert.That(divisor.Values[1], Is.EqualTo(20f).Within(0.01f));
    }
}
