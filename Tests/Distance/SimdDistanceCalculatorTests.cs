using NUnit.Framework;
using Neighborly.Distance;
using System;
using System.Linq;

namespace Neighborly.Tests.Distance;

[TestFixture]
public class SimdDistanceCalculatorTests
{
    private const float Tolerance = 0.0001f;
    
    [Test]
    public void SimdEuclideanDistance_WithSmallVectors_ReturnsCorrectResult()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1f, 7f });
        var vector2 = new Vector(new float[] { 8f, 6f });
        var calculator = new SimdEuclideanDistanceCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        Assert.That(result, Is.EqualTo(7.071068f).Within(Tolerance));
    }
    
    [Test]
    public void SimdEuclideanDistance_WithLargeVectors_ReturnsCorrectResult()
    {
        // Arrange
        var size = 100;
        var values1 = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var values2 = Enumerable.Range(0, size).Select(i => (float)(i + 1)).ToArray();
        var vector1 = new Vector(values1);
        var vector2 = new Vector(values2);
        var calculator = new SimdEuclideanDistanceCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        // Expected: sqrt(100) = 10
        Assert.That(result, Is.EqualTo(10.0f).Within(Tolerance));
    }
    
    [Test]
    public void SimdEuclideanDistance_WithNonAlignedVectorSize_ReturnsCorrectResult()
    {
        // Arrange - 17 elements (not a multiple of typical SIMD vector sizes)
        var values1 = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };
        var values2 = new float[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        var vector1 = new Vector(values1);
        var vector2 = new Vector(values2);
        var calculator = new SimdEuclideanDistanceCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        // Expected: sqrt(17) ≈ 4.123
        Assert.That(result, Is.EqualTo(MathF.Sqrt(17)).Within(Tolerance));
    }
    
    [Test]
    public void SimdManhattanDistance_ReturnsCorrectResult()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1.1234567890f, 7.1234567890f });
        var vector2 = new Vector(new float[] { 8.1234567890f, 6.1234567890f });
        var calculator = new SimdManhattanDistanceCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        Assert.That(result, Is.EqualTo(8.0f).Within(Tolerance));
    }
    
    [Test]
    public void SimdChebyshevDistance_ReturnsCorrectResult()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1f, 7f, 3f });
        var vector2 = new Vector(new float[] { 8f, 6f, 5f });
        var calculator = new SimdChebyshevDistanceCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        // Expected: max(|1-8|, |7-6|, |3-5|) = max(7, 1, 2) = 7
        Assert.That(result, Is.EqualTo(7.0f).Within(Tolerance));
    }
    
    [Test]
    public void SimdMinkowskiDistance_ReturnsCorrectResult()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1f, 2f });
        var vector2 = new Vector(new float[] { 4f, 6f });
        var calculator = new SimdMinkowskiDistanceCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        // Expected: (|1-4|^3 + |2-6|^3)^(1/3) = (27 + 64)^(1/3) = 91^(1/3) ≈ 4.498
        var expected = MathF.Pow(91f, 1f/3f);
        Assert.That(result, Is.EqualTo(expected).Within(Tolerance));
    }
    
    [Test]
    public void SimdCosineSimilarity_ReturnsCorrectResult()
    {
        // Arrange
        var vector1 = new Vector(new float[] { 1f, 3f, 3f, 7f });
        var vector2 = new Vector(new float[] { 0f, 4f, 2f, 0f });
        var calculator = new SimdCosineSimilarityCalculator();
        
        // Act
        var result = calculator.CalculateDistance(vector1, vector2);
        
        // Assert
        Assert.That(result, Is.EqualTo(0.4880f).Within(Tolerance));
    }
    
    [Test]
    public void SimdCalculators_WithIdenticalVectors_ReturnExpectedValues()
    {
        // Arrange
        var values = new float[] { 1f, 2f, 3f, 4f, 5f };
        var vector1 = new Vector(values);
        var vector2 = new Vector(values);
        
        // Act & Assert
        Assert.That(new SimdEuclideanDistanceCalculator().CalculateDistance(vector1, vector2), Is.EqualTo(0f).Within(Tolerance));
        Assert.That(new SimdManhattanDistanceCalculator().CalculateDistance(vector1, vector2), Is.EqualTo(0f).Within(Tolerance));
        Assert.That(new SimdChebyshevDistanceCalculator().CalculateDistance(vector1, vector2), Is.EqualTo(0f).Within(Tolerance));
        Assert.That(new SimdMinkowskiDistanceCalculator().CalculateDistance(vector1, vector2), Is.EqualTo(0f).Within(Tolerance));
        Assert.That(new SimdCosineSimilarityCalculator().CalculateDistance(vector1, vector2), Is.EqualTo(1f).Within(Tolerance));
    }
    
    [Test]
    public void SimdCalculators_CompareWithScalarImplementations()
    {
        // Arrange
        var size = 37; // Non-aligned size
        var random = new Random(42);
        var values1 = Enumerable.Range(0, size).Select(_ => (float)random.NextDouble() * 10).ToArray();
        var values2 = Enumerable.Range(0, size).Select(_ => (float)random.NextDouble() * 10).ToArray();
        var vector1 = new Vector(values1);
        var vector2 = new Vector(values2);
        
        // Act & Assert - Compare SIMD with scalar implementations
        var euclideanScalar = new EuclideanDistanceCalculator().CalculateDistance(vector1, vector2);
        var euclideanSimd = new SimdEuclideanDistanceCalculator().CalculateDistance(vector1, vector2);
        Assert.That(euclideanSimd, Is.EqualTo(euclideanScalar).Within(Tolerance));
        
        var manhattanScalar = new ManhattanDistanceCalculator().CalculateDistance(vector1, vector2);
        var manhattanSimd = new SimdManhattanDistanceCalculator().CalculateDistance(vector1, vector2);
        Assert.That(manhattanSimd, Is.EqualTo(manhattanScalar).Within(Tolerance));
        
        var chebyshevScalar = new ChebyshevDistanceCalculator().CalculateDistance(vector1, vector2);
        var chebyshevSimd = new SimdChebyshevDistanceCalculator().CalculateDistance(vector1, vector2);
        Assert.That(chebyshevSimd, Is.EqualTo(chebyshevScalar).Within(Tolerance));
        
        var minkowskiScalar = new MinkowskiDistanceCalculator().CalculateDistance(vector1, vector2);
        var minkowskiSimd = new SimdMinkowskiDistanceCalculator().CalculateDistance(vector1, vector2);
        Assert.That(minkowskiSimd, Is.EqualTo(minkowskiScalar).Within(Tolerance));
        
        var cosineScalar = new CosineSimilarityCalculator().CalculateDistance(vector1, vector2);
        var cosineSimd = new SimdCosineSimilarityCalculator().CalculateDistance(vector1, vector2);
        Assert.That(cosineSimd, Is.EqualTo(cosineScalar).Within(Tolerance));
    }
}