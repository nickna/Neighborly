using System.Numerics;
using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// SIMD-optimized Euclidean distance calculator for 1536-dimensional vectors.
/// </summary>
public sealed class SimdEuclideanDistance1536Calculator : AbstractDistanceCalculator
{
    /// <summary>
    /// Static instance of the calculator.
    /// </summary>
    public static readonly SimdEuclideanDistance1536Calculator Instance = new();

    private const int ExpectedDimension = 1536;

    /// <inheritdoc />
    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        if (vector1.Dimension != ExpectedDimension || vector2.Dimension != ExpectedDimension)
        {
            // Fallback to the generic implementation if dimensions do not match.
            return SimdEuclideanDistanceCalculator.Instance.CalculateDistance(vector1, vector2);
        }

        return CalculateSimd(vector1.Values, vector2.Values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateSimd(float[] values1, float[] values2)
    {
        var sumVector = Vector<float>.Zero;
        var vectorSize = Vector<float>.Count;
        var i = 0;

        var simdBoundary = ExpectedDimension - (ExpectedDimension % (vectorSize * 4));

        for (; i < simdBoundary; i += vectorSize * 4)
        {
            sumVector += (new Vector<float>(values1, i) - new Vector<float>(values2, i)) * (new Vector<float>(values1, i) - new Vector<float>(values2, i));
            sumVector += (new Vector<float>(values1, i + vectorSize) - new Vector<float>(values2, i + vectorSize)) * (new Vector<float>(values1, i + vectorSize) - new Vector<float>(values2, i + vectorSize));
            sumVector += (new Vector<float>(values1, i + vectorSize * 2) - new Vector<float>(values2, i + vectorSize * 2)) * (new Vector<float>(values1, i + vectorSize * 2) - new Vector<float>(values2, i + vectorSize * 2));
            sumVector += (new Vector<float>(values1, i + vectorSize * 3) - new Vector<float>(values2, i + vectorSize * 3)) * (new Vector<float>(values1, i + vectorSize * 3) - new Vector<float>(values2, i + vectorSize * 3));
        }

        var remainingBoundary = ExpectedDimension - (ExpectedDimension % vectorSize);
        for (; i < remainingBoundary; i += vectorSize)
        {
            var diff = new Vector<float>(values1, i) - new Vector<float>(values2, i);
            sumVector += diff * diff;
        }

        var sum = System.Numerics.Vector.Dot(sumVector, Vector<float>.One);

        for (; i < ExpectedDimension; i++)
        {
            var diff = values1[i] - values2[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }
}
