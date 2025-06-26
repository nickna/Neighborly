using System.Numerics;
using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// SIMD-optimized Cosine similarity calculator using System.Numerics.Vector&lt;T&gt;.
/// </summary>
public sealed class SimdCosineSimilarityCalculator : AbstractDistanceCalculator
{
    /// <summary>
    /// Static instance of the <see cref="SimdCosineSimilarityCalculator"/>, which can be used directly
    /// as a default for distance calculations.
    /// </summary>
    internal static SimdCosineSimilarityCalculator Instance { get; } = new SimdCosineSimilarityCalculator();

    /// <summary>
    /// Minimum vector dimension to benefit from SIMD operations.
    /// </summary>
    private const int MinimumSimdDimension = 16;

    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        var values1 = vector1.Values;
        var values2 = vector2.Values;
        var dimension = vector1.Dimension;

        // For small vectors, use scalar implementation
        if (dimension < MinimumSimdDimension || !System.Numerics.Vector.IsHardwareAccelerated)
        {
            return CalculateScalar(values1, values2, dimension);
        }

        return CalculateSimd(values1, values2, dimension);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateSimd(float[] values1, float[] values2, int dimension)
    {
        var dotProductVector = Vector<float>.Zero;
        var magnitudeAVector = Vector<float>.Zero;
        var magnitudeBVector = Vector<float>.Zero;
        var vectorSize = Vector<float>.Count;
        var i = 0;

        // Process vectors in chunks of Vector<float>.Count
        var simdBoundary = dimension - (dimension % vectorSize);
        for (; i < simdBoundary; i += vectorSize)
        {
            var vec1 = new Vector<float>(values1, i);
            var vec2 = new Vector<float>(values2, i);
            
            dotProductVector += vec1 * vec2;
            magnitudeAVector += vec1 * vec1;
            magnitudeBVector += vec2 * vec2;
        }

        // Sum all elements in the vectors
        var dotProduct = System.Numerics.Vector.Dot(dotProductVector, Vector<float>.One);
        var magnitudeA = System.Numerics.Vector.Dot(magnitudeAVector, Vector<float>.One);
        var magnitudeB = System.Numerics.Vector.Dot(magnitudeBVector, Vector<float>.One);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            dotProduct += values1[i] * values2[i];
            magnitudeA += values1[i] * values1[i];
            magnitudeB += values2[i] * values2[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);
        return dotProduct / (magnitudeA * magnitudeB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateScalar(float[] values1, float[] values2, int dimension)
    {
        var dotProduct = 0f;
        var magnitudeA = 0f;
        var magnitudeB = 0f;
        
        for (var i = 0; i < dimension; i++)
        {
            dotProduct += values1[i] * values2[i];
            magnitudeA += values1[i] * values1[i];
            magnitudeB += values2[i] * values2[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);
        return dotProduct / (magnitudeA * magnitudeB);
    }
}