using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Binary Quantization (BQ) for efficient vector storage and fast approximate search.
/// Converts floating-point vectors to binary representations, reducing memory usage by ~32x.
/// </summary>
public class BinaryQuantization
{
    /// <summary>
    /// Represents a binary-quantized vector
    /// </summary>
    public class BinaryVector
    {
        public ulong[] BinaryData { get; }
        public Vector OriginalVector { get; }
        public int Dimensions { get; }

        public BinaryVector(ulong[] binaryData, Vector originalVector, int dimensions)
        {
            BinaryData = binaryData;
            OriginalVector = originalVector;
            Dimensions = dimensions;
        }

        /// <summary>
        /// Calculate Hamming distance between two binary vectors
        /// </summary>
        public int HammingDistance(BinaryVector other)
        {
            if (BinaryData.Length != other.BinaryData.Length)
                throw new ArgumentException("Binary vectors must have the same length");

            int distance = 0;
            for (int i = 0; i < BinaryData.Length; i++)
            {
                // XOR and count set bits (popcount)
                ulong xor = BinaryData[i] ^ other.BinaryData[i];
                distance += PopCount(xor);
            }
            return distance;
        }

        private static int PopCount(ulong x)
        {
            // Brian Kernighan's algorithm for counting set bits
            int count = 0;
            while (x != 0)
            {
                count++;
                x &= x - 1;
            }
            return count;
        }
    }

    private readonly List<BinaryVector> binaryVectors;
    private readonly IDistanceCalculator distanceCalculator;
    private readonly float threshold;

    /// <summary>
    /// Initializes Binary Quantization with a list of vectors
    /// </summary>
    /// <param name="vectors">Vectors to quantize</param>
    /// <param name="threshold">Threshold for binary quantization (default: 0.0 = use mean)</param>
    /// <param name="distanceCalculator">Distance calculator for final ranking</param>
    public BinaryQuantization(VectorList vectors, float? threshold = null, IDistanceCalculator? distanceCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        
        this.distanceCalculator = distanceCalculator ?? EuclideanDistanceCalculator.Instance;
        this.binaryVectors = new List<BinaryVector>();
        
        if (vectors.Count == 0)
        {
            this.threshold = 0.0f;
            return;
        }

        // Calculate threshold (mean of all dimensions across all vectors)
        this.threshold = threshold ?? CalculateGlobalMean(vectors);
        
        // Quantize all vectors
        foreach (var vector in vectors)
        {
            var binaryVector = Quantize(vector, this.threshold);
            binaryVectors.Add(binaryVector);
        }
    }

    private static float CalculateGlobalMean(VectorList vectors)
    {
        if (vectors.Count == 0) return 0.0f;

        double sum = 0.0;
        long totalElements = 0;

        foreach (var vector in vectors)
        {
            foreach (var value in vector.Values)
            {
                sum += value;
                totalElements++;
            }
        }

        return (float)(sum / totalElements);
    }

    /// <summary>
    /// Quantizes a single vector to binary representation
    /// </summary>
    public static BinaryVector Quantize(Vector vector, float threshold)
    {
        ArgumentNullException.ThrowIfNull(vector);

        int dimensions = vector.Values.Length;
        int binaryLength = (dimensions + 63) / 64; // Round up to nearest multiple of 64
        var binaryData = new ulong[binaryLength];

        for (int i = 0; i < dimensions; i++)
        {
            if (vector.Values[i] >= threshold)
            {
                int arrayIndex = i / 64;
                int bitIndex = i % 64;
                binaryData[arrayIndex] |= (1UL << bitIndex);
            }
        }

        return new BinaryVector(binaryData, vector, dimensions);
    }

    /// <summary>
    /// Performs binary quantized search with Hamming distance filtering
    /// </summary>
    /// <param name="query">Query vector</param>
    /// <param name="k">Number of results to return</param>
    /// <param name="maxHammingDistance">Maximum Hamming distance for candidates (optional)</param>
    /// <returns>List of k nearest neighbors</returns>
    public IList<Vector> Search(Vector query, int k, int? maxHammingDistance = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        
        if (k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), "k must be greater than 0");

        if (binaryVectors.Count == 0)
            return new List<Vector>();

        // Quantize the query
        var queryBinary = Quantize(query, threshold);

        // Calculate default max Hamming distance if not provided
        int maxHamming = maxHammingDistance ?? Math.Min(query.Values.Length / 4, 64);

        // Find candidates using Hamming distance
        var candidates = new List<(BinaryVector binaryVec, int hammingDist)>();
        
        foreach (var binaryVec in binaryVectors)
        {
            int hammingDist = queryBinary.HammingDistance(binaryVec);
            if (hammingDist <= maxHamming)
            {
                candidates.Add((binaryVec, hammingDist));
            }
        }

        // If no candidates within Hamming distance, fall back to closest Hamming distances
        if (candidates.Count == 0)
        {
            candidates = binaryVectors
                .Select(bv => (bv, queryBinary.HammingDistance(bv)))
                .OrderBy(x => x.Item2)
                .Take(Math.Min(k * 3, binaryVectors.Count))
                .ToList();
        }

        // Calculate exact distances for candidates and sort
        var exactDistances = new List<(Vector vector, float distance)>();
        foreach (var (binaryVec, hammingDist) in candidates)
        {
            float exactDist = distanceCalculator.CalculateDistance(query, binaryVec.OriginalVector);
            exactDistances.Add((binaryVec.OriginalVector, exactDist));
        }

        // Sort by exact distance and return top k
        exactDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        return exactDistances
            .Take(k)
            .Select(x => x.vector)
            .ToList();
    }

    /// <summary>
    /// Gets the compression ratio achieved by binary quantization
    /// </summary>
    public float GetCompressionRatio()
    {
        if (binaryVectors.Count == 0) return 0.0f;
        
        int originalBits = binaryVectors[0].Dimensions * 32; // 32 bits per float
        int compressedBits = binaryVectors[0].BinaryData.Length * 64; // 64 bits per ulong
        
        return (float)originalBits / compressedBits;
    }

    /// <summary>
    /// Gets memory usage statistics
    /// </summary>
    public (long originalBytes, long compressedBytes, float compressionRatio) GetMemoryStats()
    {
        if (binaryVectors.Count == 0) return (0, 0, 0.0f);

        long originalBytes = binaryVectors.Count * binaryVectors[0].Dimensions * sizeof(float);
        long compressedBytes = binaryVectors.Count * binaryVectors[0].BinaryData.Length * sizeof(ulong);
        float ratio = (float)originalBytes / compressedBytes;

        return (originalBytes, compressedBytes, ratio);
    }

    /// <summary>
    /// Static search method for compatibility with SearchService pattern
    /// </summary>
    public static IList<Vector> Search(VectorList vectors, Vector query, int k)
    {
        var bq = new BinaryQuantization(vectors);
        return bq.Search(query, k);
    }
}