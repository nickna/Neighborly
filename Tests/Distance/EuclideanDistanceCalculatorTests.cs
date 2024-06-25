namespace Neighborly.Distance.Tests;

[TestFixture]
public class EuclideanDistanceCalculatorTests
{
    [Test]
    public void CalculateDistanceCore_WhenCalled_Verified()
    {
        // Arrange
        EuclideanDistanceCalculator calculator = new();
        Vector vector1 = new([1f, 7f]);
        Vector vector2 = new([8f, 6f]);

        // Act
        float result = calculator.CalculateDistance(vector1, vector2);

        // Assert
        Assert.That(result, Is.EqualTo(7.07107f).Within(0.0001f));
    }
}
