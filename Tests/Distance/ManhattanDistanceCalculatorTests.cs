namespace Neighborly.Distance.Tests;

[TestFixture]
public class ManhattanDistanceCalculatorTests
{
    [Test]
    public void CalculateDistanceCore_WhenCalled_Verified()
    {
        // Arrange
        ManhattanDistanceCalculator calculator = new();
        Vector vector1 = new([0.7438571528f, 0.6425611614f, 0.5911289441f, 0.3370079785f]);
        Vector vector2 = new([0.7079857467f, 0.2068195298f, 0.9294371761f, 0.3847158399f]);

        // Act
        float result = calculator.CalculateDistance(vector1, vector2);

        // Assert
        Assert.That(result, Is.EqualTo(0.857629f).Within(0.0001f));
    }
}
