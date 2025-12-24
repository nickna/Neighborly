using System.Numerics;
using System.Runtime.CompilerServices;

namespace Neighborly.Distance;

/// <summary>
/// SIMD-optimized Euclidean distance calculator for specific vector dimensions.
/// This generic implementation replaces individual dimension-specific calculators
/// with a single reusable class.
/// </summary>
/// <typeparam name="TDimension">A type that provides the expected dimension constant.</typeparam>
public class SimdDimensionSpecificEuclideanCalculator<TDimension> : AbstractBatchDistanceCalculator
    where TDimension : IDimensionProvider
{
    private static readonly int ExpectedDimension = TDimension.Dimension;

    /// <summary>
    /// Static instance of the calculator for the specific dimension.
    /// </summary>
    public static readonly SimdDimensionSpecificEuclideanCalculator<TDimension> Instance = new();

    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        // Fallback to generic SIMD implementation if dimensions don't match
        if (vector1.Dimension != ExpectedDimension || vector2.Dimension != ExpectedDimension)
        {
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

        // 4x loop unrolling for maximum performance
        var simdBoundary = ExpectedDimension - (ExpectedDimension % (vectorSize * 4));

        for (; i < simdBoundary; i += vectorSize * 4)
        {
            // Unroll 4 iterations for better instruction-level parallelism
            var diff0 = new Vector<float>(values1, i) - new Vector<float>(values2, i);
            var diff1 = new Vector<float>(values1, i + vectorSize) - new Vector<float>(values2, i + vectorSize);
            var diff2 = new Vector<float>(values1, i + vectorSize * 2) - new Vector<float>(values2, i + vectorSize * 2);
            var diff3 = new Vector<float>(values1, i + vectorSize * 3) - new Vector<float>(values2, i + vectorSize * 3);

            sumVector += diff0 * diff0;
            sumVector += diff1 * diff1;
            sumVector += diff2 * diff2;
            sumVector += diff3 * diff3;
        }

        // Handle remaining full vectors
        var remainingBoundary = ExpectedDimension - (ExpectedDimension % vectorSize);
        for (; i < remainingBoundary; i += vectorSize)
        {
            var diff = new Vector<float>(values1, i) - new Vector<float>(values2, i);
            sumVector += diff * diff;
        }

        // Sum vector elements
        var sum = System.Numerics.Vector.Dot(sumVector, Vector<float>.One);

        // Handle remaining scalar elements
        for (; i < ExpectedDimension; i++)
        {
            var diff = values1[i] - values2[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }
}

/// <summary>
/// Provides dimension information for dimension-specific calculators.
/// </summary>
public interface IDimensionProvider
{
    /// <summary>
    /// The expected vector dimension.
    /// </summary>
    static abstract int Dimension { get; }
}

/// <summary>
/// Dimension provider for 128-dimensional vectors.
/// </summary>
public readonly struct Dimension128 : IDimensionProvider
{
    public static int Dimension => 128;
}

/// <summary>
/// Dimension provider for 256-dimensional vectors.
/// </summary>
public readonly struct Dimension256 : IDimensionProvider
{
    public static int Dimension => 256;
}

/// <summary>
/// Dimension provider for 384-dimensional vectors.
/// </summary>
public readonly struct Dimension384 : IDimensionProvider
{
    public static int Dimension => 384;
}

/// <summary>
/// Dimension provider for 512-dimensional vectors.
/// </summary>
public readonly struct Dimension512 : IDimensionProvider
{
    public static int Dimension => 512;
}

/// <summary>
/// Dimension provider for 768-dimensional vectors.
/// </summary>
public readonly struct Dimension768 : IDimensionProvider
{
    public static int Dimension => 768;
}

/// <summary>
/// Dimension provider for 1024-dimensional vectors.
/// </summary>
public readonly struct Dimension1024 : IDimensionProvider
{
    public static int Dimension => 1024;
}

/// <summary>
/// Dimension provider for 1536-dimensional vectors (OpenAI ada-002).
/// </summary>
public readonly struct Dimension1536 : IDimensionProvider
{
    public static int Dimension => 1536;
}

// Backward compatibility: explicit class aliases for common dimensions
/// <summary>SIMD-optimized Euclidean distance calculator for 128-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance128Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension128>
{
    public new static readonly SimdEuclideanDistance128Calculator Instance = new();
}

/// <summary>SIMD-optimized Euclidean distance calculator for 256-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance256Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension256>
{
    public new static readonly SimdEuclideanDistance256Calculator Instance = new();
}

/// <summary>SIMD-optimized Euclidean distance calculator for 384-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance384Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension384>
{
    public new static readonly SimdEuclideanDistance384Calculator Instance = new();
}

/// <summary>SIMD-optimized Euclidean distance calculator for 512-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance512Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension512>
{
    public new static readonly SimdEuclideanDistance512Calculator Instance = new();
}

/// <summary>SIMD-optimized Euclidean distance calculator for 768-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance768Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension768>
{
    public new static readonly SimdEuclideanDistance768Calculator Instance = new();
}

/// <summary>SIMD-optimized Euclidean distance calculator for 1024-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance1024Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension1024>
{
    public new static readonly SimdEuclideanDistance1024Calculator Instance = new();
}

/// <summary>SIMD-optimized Euclidean distance calculator for 1536-dimensional vectors.</summary>
public sealed class SimdEuclideanDistance1536Calculator : SimdDimensionSpecificEuclideanCalculator<Dimension1536>
{
    public new static readonly SimdEuclideanDistance1536Calculator Instance = new();
}
