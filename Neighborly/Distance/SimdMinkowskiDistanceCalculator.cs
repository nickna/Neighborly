using System.Numerics;
using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// SIMD-optimized Minkowski distance calculator using System.Numerics.Vector&lt;T&gt;.
/// </summary>
public sealed class SimdMinkowskiDistanceCalculator : AbstractDistanceCalculator
{
    /// <summary>
    /// Static instance of the <see cref="SimdMinkowskiDistanceCalculator"/>, which can be used directly
    /// as a default for distance calculations.
    /// </summary>
    internal static SimdMinkowskiDistanceCalculator Instance { get; } = new SimdMinkowskiDistanceCalculator();

    /// <summary>
    /// Minimum vector dimension to benefit from SIMD operations.
    /// </summary>
    private const int MinimumSimdDimension = 16;

    /// <summary>
    /// The order of the Minkowski distance (p=3 for the default implementation).
    /// </summary>
    private const float P = 3.0f;

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
        var sumVector = Vector<float>.Zero;
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
            
            // For p=3, we need to compute |diff|^3
            // We can do this by multiplying absDiff * absDiff * absDiff
            sumVector += absDiff * absDiff * absDiff;
        }

        // Sum all elements in the vector
        var sum = System.Numerics.Vector.Dot(sumVector, Vector<float>.One);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            var diff = values1[i] - values2[i];
            sum += MathF.Pow(MathF.Abs(diff), P);
        }

        return MathF.Pow(sum, 1.0f / P);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateScalar(float[] values1, float[] values2, int dimension)
    {
        var sum = 0f;
        for (var i = 0; i < dimension; i++)
        {
            var diff = values1[i] - values2[i];
            sum += MathF.Pow(MathF.Abs(diff), P);
        }
        return MathF.Pow(sum, 1.0f / P);
    }
}