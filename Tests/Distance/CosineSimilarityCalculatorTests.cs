namespace Neighborly.Distance.Tests;

[TestFixture]
public class CosineSimilarityCalculatorTests
{
    [Test]
    public void CalculateDistanceCore_WhenCalled_Verified()
    {
        // Arrange
        CosineSimilarityCalculator calculator = new();
        Vector vector1 = new([1f, 3f, 3f, 7f]);
        Vector vector2 = new([0f, 4f, 2f, 0f]);

        // Act
        float result = calculator.CalculateDistance(vector1, vector2);

        // Assert
        Assert.That(result, Is.EqualTo(0.4880f).Within(0.0001f));
    }
}
