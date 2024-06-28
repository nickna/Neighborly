using Neighborly.Distance;

namespace Neighborly.Tests.Distance;

[TestFixture]
public class AbstractDistanceCalculatorTests
{
    [Test]
    public void CalculateDistance_WhenCalled_Verified_First_Vector()
    {
        // Arrange
        DistanceCalculator distanceCalculator = new();
        Vector vector2 = new([1f]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => distanceCalculator.CalculateDistance(null!, vector2));
    }

    [Test]
    public void CalculateDistance_WhenCalled_Verified_Second_Vector()
    {
        // Arrange
        DistanceCalculator distanceCalculator = new();
        Vector vector1 = new([1f]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => distanceCalculator.CalculateDistance(vector1, null!));
    }

    [Test]
    public void CalculateDistance_WhenCalled_Ensure_Vector_Dimensions()
    {
        // Arrange
        DistanceCalculator distanceCalculator = new();
        Vector vector1 = new([1f]);
        Vector vector2 = new([1f, 2f]);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => distanceCalculator.CalculateDistance(vector1, vector2));
    }

    private class DistanceCalculator : AbstractDistanceCalculator
    {
        protected override float CalculateDistanceCore(Vector vector1, Vector vector2) => float.Pi;
    }

}