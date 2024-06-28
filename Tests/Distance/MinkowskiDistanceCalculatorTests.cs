using Neighborly.Distance;

namespace Neighborly.Tests.Distance;

[TestFixture]
public class MinkowskiDistanceCalculatorTests
{
    [Test]
    public void CalculateDistanceCore_WhenCalled_Verified()
    {
        // Arrange
        MinkowskiDistanceCalculator calculator = new();
        Vector vector1 = new([0f, 3f, 4f, 5f]);
        Vector vector2 = new([7f, 6f, 3f, -1f]);

        // Act
        float result = calculator.CalculateDistance(vector1, vector2);

        // Assert
        Assert.That(result, Is.EqualTo(8.373f).Within(0.0001f));
    }
}
