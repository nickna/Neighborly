using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Neighborly.Distance;

/// <summary>
/// Cache-optimized Euclidean distance calculator with SIMD support.
/// </summary>
public sealed class CacheOptimizedEuclideanDistance : IDistanceCalculator
{
    public static readonly CacheOptimizedEuclideanDistance Instance = new();

    public float CalculateDistance(Vector vector1, Vector vector2)
    {
        // Convert to cache-optimized vectors if needed
        using var opt1 = CacheOptimizedVector.FromVector(vector1);
        using var opt2 = CacheOptimizedVector.FromVector(vector2);

        return CalculateDistance(opt1, opt2);
    }

    public unsafe float CalculateDistance(CacheOptimizedVector vector1, CacheOptimizedVector vector2)
    {
        if (vector1.Dimension != vector2.Dimension)
            throw new ArgumentException("Vectors must have the same dimension");

        float* ptr1 = vector1.GetAlignedPointer();
        float* ptr2 = vector2.GetAlignedPointer();
        int dimension = vector1.Dimension;

        // Use SIMD if available - prefer AVX512 > AVX > SSE
        if (Avx512F.IsSupported)
        {
            return CalculateDistanceAvx512(ptr1, ptr2, dimension);
        }
        else if (Avx.IsSupported)
        {
            return CalculateDistanceAvx(ptr1, ptr2, dimension);
        }
        else if (Sse.IsSupported)
        {
            return CalculateDistanceSse(ptr1, ptr2, dimension);
        }
        else
        {
            return CalculateDistanceScalar(ptr1, ptr2, dimension);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateDistanceAvx512(float* ptr1, float* ptr2, int dimension)
    {
        Vector512<float> sumVec = Vector512<float>.Zero;
        int i = 0;

        // Process 16 floats at a time (AVX-512 operates on 512-bit registers)
        int simdLength = dimension - (dimension % 16);
        for (; i < simdLength; i += 16)
        {
            var vec1 = Avx512F.LoadAlignedVector512(ptr1 + i);
            var vec2 = Avx512F.LoadAlignedVector512(ptr2 + i);
            var diff = Avx512F.Subtract(vec1, vec2);
            var squared = Avx512F.Multiply(diff, diff);
            sumVec = Avx512F.Add(sumVec, squared);
        }

        // Use modern Vector512.Sum() - no heap allocation!
        float sum = Vector512.Sum(sumVec);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            float diff = ptr1[i] - ptr2[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateDistanceAvx(float* ptr1, float* ptr2, int dimension)
    {
        Vector256<float> sumVec = Vector256<float>.Zero;
        int i = 0;

        // Process 8 floats at a time (AVX operates on 256-bit registers)
        int simdLength = dimension - (dimension % 8);
        for (; i < simdLength; i += 8)
        {
            var vec1 = Avx.LoadAlignedVector256(ptr1 + i);
            var vec2 = Avx.LoadAlignedVector256(ptr2 + i);
            var diff = Avx.Subtract(vec1, vec2);
            var squared = Avx.Multiply(diff, diff);
            sumVec = Avx.Add(sumVec, squared);
        }

        // Use modern Vector256.Sum() instead of heap-allocating array - zero allocations!
        float sum = Vector256.Sum(sumVec);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            float diff = ptr1[i] - ptr2[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateDistanceSse(float* ptr1, float* ptr2, int dimension)
    {
        Vector128<float> sumVec = Vector128<float>.Zero;
        int i = 0;

        // Process 4 floats at a time
        int simdLength = dimension - (dimension % 4);
        for (; i < simdLength; i += 4)
        {
            var vec1 = Sse.LoadAlignedVector128(ptr1 + i);
            var vec2 = Sse.LoadAlignedVector128(ptr2 + i);
            var diff = Sse.Subtract(vec1, vec2);
            var squared = Sse.Multiply(diff, diff);
            sumVec = Sse.Add(sumVec, squared);
        }

        // Use modern Vector128.Sum() instead of heap-allocating array - zero allocations!
        float sum = Vector128.Sum(sumVec);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            float diff = ptr1[i] - ptr2[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateDistanceScalar(float* ptr1, float* ptr2, int dimension)
    {
        float sum = 0;

        // Unroll loop for better performance
        int i = 0;
        int unrolledLength = dimension - (dimension % 4);

        for (; i < unrolledLength; i += 4)
        {
            float diff0 = ptr1[i] - ptr2[i];
            float diff1 = ptr1[i + 1] - ptr2[i + 1];
            float diff2 = ptr1[i + 2] - ptr2[i + 2];
            float diff3 = ptr1[i + 3] - ptr2[i + 3];

            sum += diff0 * diff0;
            sum += diff1 * diff1;
            sum += diff2 * diff2;
            sum += diff3 * diff3;
        }

        // Process remaining elements
        for (; i < dimension; i++)
        {
            float diff = ptr1[i] - ptr2[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }
}

/// <summary>
/// Cache-optimized cosine similarity calculator with SIMD support.
/// </summary>
public sealed class CacheOptimizedCosineSimilarity : IDistanceCalculator
{
    public static readonly CacheOptimizedCosineSimilarity Instance = new();

    public float CalculateDistance(Vector vector1, Vector vector2)
    {
        using var opt1 = CacheOptimizedVector.FromVector(vector1);
        using var opt2 = CacheOptimizedVector.FromVector(vector2);

        return CalculateDistance(opt1, opt2);
    }

    public unsafe float CalculateDistance(CacheOptimizedVector vector1, CacheOptimizedVector vector2)
    {
        if (vector1.Dimension != vector2.Dimension)
            throw new ArgumentException("Vectors must have the same dimension");

        float* ptr1 = vector1.GetAlignedPointer();
        float* ptr2 = vector2.GetAlignedPointer();
        int dimension = vector1.Dimension;

        // Use SIMD if available - prefer AVX512 > AVX > SSE
        if (Avx512F.IsSupported)
        {
            return CalculateSimilarityAvx512(ptr1, ptr2, dimension);
        }
        else if (Avx.IsSupported)
        {
            return CalculateSimilarityAvx(ptr1, ptr2, dimension);
        }
        else if (Sse.IsSupported)
        {
            return CalculateSimilaritySse(ptr1, ptr2, dimension);
        }
        else
        {
            return CalculateSimilarityScalar(ptr1, ptr2, dimension);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateSimilarityAvx512(float* ptr1, float* ptr2, int dimension)
    {
        Vector512<float> dotProdVec = Vector512<float>.Zero;
        Vector512<float> mag1Vec = Vector512<float>.Zero;
        Vector512<float> mag2Vec = Vector512<float>.Zero;

        int i = 0;
        int simdLength = dimension - (dimension % 16);

        for (; i < simdLength; i += 16)
        {
            var vec1 = Avx512F.LoadAlignedVector512(ptr1 + i);
            var vec2 = Avx512F.LoadAlignedVector512(ptr2 + i);

            dotProdVec = Avx512F.Add(dotProdVec, Avx512F.Multiply(vec1, vec2));
            mag1Vec = Avx512F.Add(mag1Vec, Avx512F.Multiply(vec1, vec1));
            mag2Vec = Avx512F.Add(mag2Vec, Avx512F.Multiply(vec2, vec2));
        }

        // Use modern Vector512.Sum() - no heap allocations!
        float dotProduct = Vector512.Sum(dotProdVec);
        float magnitude1 = Vector512.Sum(mag1Vec);
        float magnitude2 = Vector512.Sum(mag2Vec);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            float v1 = ptr1[i];
            float v2 = ptr2[i];
            dotProduct += v1 * v2;
            magnitude1 += v1 * v1;
            magnitude2 += v2 * v2;
        }

        return dotProduct / (MathF.Sqrt(magnitude1) * MathF.Sqrt(magnitude2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateSimilarityAvx(float* ptr1, float* ptr2, int dimension)
    {
        Vector256<float> dotProdVec = Vector256<float>.Zero;
        Vector256<float> mag1Vec = Vector256<float>.Zero;
        Vector256<float> mag2Vec = Vector256<float>.Zero;

        int i = 0;
        int simdLength = dimension - (dimension % 8);

        for (; i < simdLength; i += 8)
        {
            var vec1 = Avx.LoadAlignedVector256(ptr1 + i);
            var vec2 = Avx.LoadAlignedVector256(ptr2 + i);

            dotProdVec = Avx.Add(dotProdVec, Avx.Multiply(vec1, vec2));
            mag1Vec = Avx.Add(mag1Vec, Avx.Multiply(vec1, vec1));
            mag2Vec = Avx.Add(mag2Vec, Avx.Multiply(vec2, vec2));
        }

        // Use modern Vector256.Sum() instead of heap-allocating array - zero allocations!
        float dotProduct = Vector256.Sum(dotProdVec);
        float magnitude1 = Vector256.Sum(mag1Vec);
        float magnitude2 = Vector256.Sum(mag2Vec);

        // Process remaining elements
        for (; i < dimension; i++)
        {
            float v1 = ptr1[i];
            float v2 = ptr2[i];
            dotProduct += v1 * v2;
            magnitude1 += v1 * v1;
            magnitude2 += v2 * v2;
        }

        return dotProduct / (MathF.Sqrt(magnitude1) * MathF.Sqrt(magnitude2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateSimilaritySse(float* ptr1, float* ptr2, int dimension)
    {
        Vector128<float> dotProdVec = Vector128<float>.Zero;
        Vector128<float> mag1Vec = Vector128<float>.Zero;
        Vector128<float> mag2Vec = Vector128<float>.Zero;

        int i = 0;
        int simdLength = dimension - (dimension % 4);

        for (; i < simdLength; i += 4)
        {
            var vec1 = Sse.LoadAlignedVector128(ptr1 + i);
            var vec2 = Sse.LoadAlignedVector128(ptr2 + i);

            dotProdVec = Sse.Add(dotProdVec, Sse.Multiply(vec1, vec2));
            mag1Vec = Sse.Add(mag1Vec, Sse.Multiply(vec1, vec1));
            mag2Vec = Sse.Add(mag2Vec, Sse.Multiply(vec2, vec2));
        }

        // Use modern Vector128.Sum() instead of heap-allocating array - zero allocations!
        float dotProduct = Vector128.Sum(dotProdVec);
        float magnitude1 = Vector128.Sum(mag1Vec);
        float magnitude2 = Vector128.Sum(mag2Vec);

        // Process remaining
        for (; i < dimension; i++)
        {
            float v1 = ptr1[i];
            float v2 = ptr2[i];
            dotProduct += v1 * v2;
            magnitude1 += v1 * v1;
            magnitude2 += v2 * v2;
        }

        return dotProduct / (MathF.Sqrt(magnitude1) * MathF.Sqrt(magnitude2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe float CalculateSimilarityScalar(float* ptr1, float* ptr2, int dimension)
    {
        float dotProduct = 0;
        float magnitude1 = 0;
        float magnitude2 = 0;

        // Unroll for better performance
        int i = 0;
        int unrolledLength = dimension - (dimension % 4);

        for (; i < unrolledLength; i += 4)
        {
            float v1_0 = ptr1[i], v2_0 = ptr2[i];
            float v1_1 = ptr1[i + 1], v2_1 = ptr2[i + 1];
            float v1_2 = ptr1[i + 2], v2_2 = ptr2[i + 2];
            float v1_3 = ptr1[i + 3], v2_3 = ptr2[i + 3];

            dotProduct += v1_0 * v2_0 + v1_1 * v2_1 + v1_2 * v2_2 + v1_3 * v2_3;
            magnitude1 += v1_0 * v1_0 + v1_1 * v1_1 + v1_2 * v1_2 + v1_3 * v1_3;
            magnitude2 += v2_0 * v2_0 + v2_1 * v2_1 + v2_2 * v2_2 + v2_3 * v2_3;
        }

        // Process remaining
        for (; i < dimension; i++)
        {
            float v1 = ptr1[i];
            float v2 = ptr2[i];
            dotProduct += v1 * v2;
            magnitude1 += v1 * v1;
            magnitude2 += v2 * v2;
        }

        return dotProduct / (MathF.Sqrt(magnitude1) * MathF.Sqrt(magnitude2));
    }
}
