using System.Numerics;
using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// SIMD-optimized Cosine similarity calculator for 128-dimensional vectors.
/// </summary>
public sealed class SimdCosineSimilarity128Calculator : AbstractDistanceCalculator
{
    /// <summary>
    /// Static instance of the calculator.
    /// </summary>
    public static readonly SimdCosineSimilarity128Calculator Instance = new();

    private const int ExpectedDimension = 128;

    /// <inheritdoc />
    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        if (vector1.Dimension != ExpectedDimension || vector2.Dimension != ExpectedDimension)
        {
            // Fallback to the generic implementation if dimensions do not match.
            return SimdCosineSimilarityCalculator.Instance.CalculateDistance(vector1, vector2);
        }

        return CalculateSimd(vector1.Values, vector2.Values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateSimd(float[] values1, float[] values2)
    {
        var dotProductVector = Vector<float>.Zero;
        var magnitudeAVector = Vector<float>.Zero;
        var magnitudeBVector = Vector<float>.Zero;
        var vectorSize = Vector<float>.Count;
        var i = 0;

        var simdBoundary = ExpectedDimension - (ExpectedDimension % (vectorSize * 4));

        for (; i < simdBoundary; i += vectorSize * 4)
        {
            var vec1_0 = new Vector<float>(values1, i);
            var vec2_0 = new Vector<float>(values2, i);
            dotProductVector += vec1_0 * vec2_0;
            magnitudeAVector += vec1_0 * vec1_0;
            magnitudeBVector += vec2_0 * vec2_0;

            var vec1_1 = new Vector<float>(values1, i + vectorSize);
            var vec2_1 = new Vector<float>(values2, i + vectorSize);
            dotProductVector += vec1_1 * vec2_1;
            magnitudeAVector += vec1_1 * vec1_1;
            magnitudeBVector += vec2_1 * vec2_1;

            var vec1_2 = new Vector<float>(values1, i + vectorSize * 2);
            var vec2_2 = new Vector<float>(values2, i + vectorSize * 2);
            dotProductVector += vec1_2 * vec2_2;
            magnitudeAVector += vec1_2 * vec1_2;
            magnitudeBVector += vec2_2 * vec2_2;

            var vec1_3 = new Vector<float>(values1, i + vectorSize * 3);
            var vec2_3 = new Vector<float>(values2, i + vectorSize * 3);
            dotProductVector += vec1_3 * vec2_3;
            magnitudeAVector += vec1_3 * vec1_3;
            magnitudeBVector += vec2_3 * vec2_3;
        }

        var remainingBoundary = ExpectedDimension - (ExpectedDimension % vectorSize);
        for (; i < remainingBoundary; i += vectorSize)
        {
            var vec1 = new Vector<float>(values1, i);
            var vec2 = new Vector<float>(values2, i);
            dotProductVector += vec1 * vec2;
            magnitudeAVector += vec1 * vec1;
            magnitudeBVector += vec2 * vec2;
        }

        var dotProduct = System.Numerics.Vector.Dot(dotProductVector, Vector<float>.One);
        var magnitudeA = System.Numerics.Vector.Dot(magnitudeAVector, Vector<float>.One);
        var magnitudeB = System.Numerics.Vector.Dot(magnitudeBVector, Vector<float>.One);

        for (; i < ExpectedDimension; i++)
        {
            dotProduct += values1[i] * values2[i];
            magnitudeA += values1[i] * values1[i];
            magnitudeB += values2[i] * values2[i];
        }

        return dotProduct / (MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB));
    }
}
