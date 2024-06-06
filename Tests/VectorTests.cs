using Neighborly;

[TestFixture]
public class VectorTests
{
    [Test]
    public void ToBinary_CanBeUsedAsInputFor_Ctor()
    {
        // Arrange
        float[] floatArray = [1.0f, 2.1f, 3.2f, 4.5f, 5.7f];
        Vector originalVector = new(floatArray, "Test");

        // Act
        byte[] binary = originalVector.ToBinary();
        Vector newVector = new(binary);
        byte[] newBinary = newVector.ToBinary();

        // Assert
        Assert.That(newBinary, Is.EqualTo(binary));
    }
}
