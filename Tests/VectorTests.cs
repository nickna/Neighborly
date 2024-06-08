using Neighborly;

[TestFixture]
public class VectorTests
{
    [Test]
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
