using System.Numerics;
using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// SIMD-optimized Chebyshev distance calculator using System.Numerics.Vector&lt;T&gt;.
/// </summary>
public sealed class SimdChebyshevDistanceCalculator : AbstractDistanceCalculator
{
    /// <summary>
    /// Static instance of the <see cref="SimdChebyshevDistanceCalculator"/>, which can be used directly
    /// as a default for distance calculations.
    /// </summary>
    internal static SimdChebyshevDistanceCalculator Instance { get; } = new SimdChebyshevDistanceCalculator();

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
        var maxVector = Vector<float>.Zero;
        var vectorSize = Vector<float>.Count;
        var i = 0;

        // Process vectors in chunks of Vector<float>.Count
        var simdBoundary = dimension - (dimension % vectorSize);
        for (; i < simdBoundary; i += vectorSize)
        {
            var vec1 = new Vector<float>(values1, i);
            var vec2 = new Vector<float>(values2, i);
            var diff = vec1 - vec2;
            var absDiff = System.Numerics.Vector.Abs(diff);
            maxVector = System.Numerics.Vector.Max(maxVector, absDiff);
        }

        // Find the maximum value in the vector
        var max = 0f;
        var maxArray = new float[vectorSize];
        maxVector.CopyTo(maxArray);
        for (var j = 0; j < vectorSize; j++)
        {
            if (maxArray[j] > max)
            {
                max = maxArray[j];
            }
        }

        // Process remaining elements
        for (; i < dimension; i++)
        {
            var diff = MathF.Abs(values1[i] - values2[i]);
            if (diff > max)
            {
                max = diff;
            }
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateScalar(float[] values1, float[] values2, int dimension)
    {
        var max = 0f;
        for (var i = 0; i < dimension; i++)
        {
            var diff = MathF.Abs(values1[i] - values2[i]);
            if (diff > max)
            {
                max = diff;
            }
        }
        return max;
    }
}