using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Product Quantization (PQ) for efficient vector compression and fast approximate search.
/// Divides high-dimensional vectors into sub-vectors and quantizes each independently,
/// achieving better compression than binary quantization while maintaining good accuracy.
/// </summary>
public class ProductQuantization
{
    /// <summary>
    /// Represents a product-quantized vector
    /// </summary>
    public class PQVector
    {
        public byte[] Codes { get; }
        public Vector OriginalVector { get; }
        public int Dimensions { get; }

        public PQVector(byte[] codes, Vector originalVector, int dimensions)
        {
            Codes = codes;
            OriginalVector = originalVector;
            Dimensions = dimensions;
        }
    }

    /// <summary>
    /// Codebook for a single sub-vector quantizer
    /// </summary>
    private class Codebook
    {
        public float[][] Centroids { get; }
        public int SubVectorDimensions { get; }
        public int NumCentroids { get; }

        public Codebook(float[][] centroids, int subVectorDimensions)
        {
            Centroids = centroids;
            SubVectorDimensions = subVectorDimensions;
            NumCentroids = centroids.Length;
        }

        public byte QuantizeSubVector(float[] subVector)
        {
            if (subVector.Length != SubVectorDimensions)
                throw new ArgumentException($"Sub-vector must have {SubVectorDimensions} dimensions");

            float minDistance = float.MaxValue;
            byte bestCentroid = 0;

            for (int i = 0; i < NumCentroids; i++)
            {
                float distance = EuclideanDistance(subVector, Centroids[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestCentroid = (byte)i;
                }
            }

            return bestCentroid;
        }

        public float[] ReconstructSubVector(byte code)
        {
            if (code >= NumCentroids)
                throw new ArgumentException($"Code {code} is invalid for codebook with {NumCentroids} centroids");

            return Centroids[code];
        }

        private static float EuclideanDistance(float[] a, float[] b)
        {
            float sum = 0.0f;
            for (int i = 0; i < a.Length; i++)
            {
                float diff = a[i] - b[i];
                sum += diff * diff;
            }
            return (float)Math.Sqrt(sum);
        }
    }

    private readonly List<PQVector> pqVectors;
    private readonly Codebook[] codebooks;
    private readonly IDistanceCalculator distanceCalculator;
    private readonly int numSubVectors;
    private readonly int subVectorDimensions;
    private readonly int numCentroids;

    /// <summary>
    /// Initializes Product Quantization with a list of vectors
    /// </summary>
    /// <param name="vectors">Vectors to quantize</param>
    /// <param name="numSubVectors">Number of sub-vectors to divide each vector into (default: auto)</param>
    /// <param name="numCentroids">Number of centroids per codebook (default: 256, max: 256)</param>
    /// <param name="distanceCalculator">Distance calculator for final ranking</param>
    /// <param name="maxIterations">Maximum k-means iterations for codebook training</param>
    public ProductQuantization(VectorList vectors, int? numSubVectors = null, int numCentroids = 256,
                              IDistanceCalculator? distanceCalculator = null, int maxIterations = 50)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        
        if (numCentroids > 256)
            throw new ArgumentException("Number of centroids cannot exceed 256 (byte limit)");

        this.distanceCalculator = distanceCalculator ?? EuclideanDistanceCalculator.Instance;
        this.numCentroids = numCentroids;
        this.pqVectors = new List<PQVector>();

        if (vectors.Count == 0)
        {
            this.numSubVectors = 1;
            this.subVectorDimensions = 1;
            this.codebooks = Array.Empty<Codebook>();
            return;
        }

        int vectorDimensions = vectors[0].Values.Length;
        
        // Auto-determine number of sub-vectors if not specified
        this.numSubVectors = numSubVectors ?? CalculateOptimalSubVectors(vectorDimensions);
        this.subVectorDimensions = vectorDimensions / this.numSubVectors;

        // Ensure dimensions are evenly divisible
        if (vectorDimensions % this.numSubVectors != 0)
        {
            throw new ArgumentException($"Vector dimensions ({vectorDimensions}) must be evenly divisible by number of sub-vectors ({this.numSubVectors})");
        }

        // Train codebooks
        this.codebooks = TrainCodebooks(vectors, maxIterations);

        // Quantize all vectors
        foreach (var vector in vectors)
        {
            var pqVector = Quantize(vector);
            pqVectors.Add(pqVector);
        }
    }

    private static int CalculateOptimalSubVectors(int dimensions)
    {
        // Heuristic: aim for sub-vectors of size 4-16 dimensions
        for (int subVectors = dimensions / 16; subVectors >= 1; subVectors--)
        {
            if (dimensions % subVectors == 0)
            {
                int subVecDim = dimensions / subVectors;
                if (subVecDim >= 4 && subVecDim <= 16)
                    return subVectors;
            }
        }

        // Fallback: find largest divisor that gives reasonable sub-vector size
        for (int subVectors = dimensions / 8; subVectors >= 1; subVectors--)
        {
            if (dimensions % subVectors == 0)
                return subVectors;
        }

        return 1; // Last resort
    }

    private Codebook[] TrainCodebooks(VectorList vectors, int maxIterations)
    {
        var codebooks = new Codebook[numSubVectors];
        var random = new Random(42);

        for (int subVectorIndex = 0; subVectorIndex < numSubVectors; subVectorIndex++)
        {
            // Extract sub-vectors for this position
            var subVectors = new List<float[]>();
            for (int vectorIndex = 0; vectorIndex < vectors.Count; vectorIndex++)
            {
                var subVector = ExtractSubVector(vectors[vectorIndex].Values, subVectorIndex);
                subVectors.Add(subVector);
            }

            // Train codebook using k-means
            var centroids = TrainKMeans(subVectors, numCentroids, maxIterations, random);
            codebooks[subVectorIndex] = new Codebook(centroids, subVectorDimensions);
        }

        return codebooks;
    }

    private float[][] TrainKMeans(List<float[]> data, int k, int maxIterations, Random random)
    {
        if (data.Count == 0) return Array.Empty<float[]>();
        if (k >= data.Count) k = data.Count;

        int dimensions = data[0].Length;
        var centroids = new float[k][];
        var assignments = new int[data.Count];

        // Initialize centroids randomly
        for (int i = 0; i < k; i++)
        {
            centroids[i] = new float[dimensions];
            var randomData = data[random.Next(data.Count)];
            Array.Copy(randomData, centroids[i], dimensions);
        }

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool changed = false;

            // Assign data points to nearest centroids
            for (int i = 0; i < data.Count; i++)
            {
                float minDistance = float.MaxValue;
                int bestCentroid = 0;

                for (int j = 0; j < k; j++)
                {
                    float distance = EuclideanDistanceSquared(data[i], centroids[j]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestCentroid = j;
                    }
                }

                if (assignments[i] != bestCentroid)
                {
                    assignments[i] = bestCentroid;
                    changed = true;
                }
            }

            if (!changed) break;

            // Update centroids
            for (int j = 0; j < k; j++)
            {
                var sum = new float[dimensions];
                int count = 0;

                for (int i = 0; i < data.Count; i++)
                {
                    if (assignments[i] == j)
                    {
                        for (int d = 0; d < dimensions; d++)
                        {
                            sum[d] += data[i][d];
                        }
                        count++;
                    }
                }

                if (count > 0)
                {
                    for (int d = 0; d < dimensions; d++)
                    {
                        centroids[j][d] = sum[d] / count;
                    }
                }
            }
        }

        return centroids;
    }

    private static float EuclideanDistanceSquared(float[] a, float[] b)
    {
        float sum = 0.0f;
        for (int i = 0; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }
        return sum;
    }

    private float[] ExtractSubVector(float[] vector, int subVectorIndex)
    {
        int start = subVectorIndex * subVectorDimensions;
        var subVector = new float[subVectorDimensions];
        Array.Copy(vector, start, subVector, 0, subVectorDimensions);
        return subVector;
    }

    /// <summary>
    /// Quantizes a single vector using the trained codebooks
    /// </summary>
    public PQVector Quantize(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Values.Length != numSubVectors * subVectorDimensions)
        {
            throw new ArgumentException($"Vector must have {numSubVectors * subVectorDimensions} dimensions");
        }

        var codes = new byte[numSubVectors];
        for (int i = 0; i < numSubVectors; i++)
        {
            var subVector = ExtractSubVector(vector.Values, i);
            codes[i] = codebooks[i].QuantizeSubVector(subVector);
        }

        return new PQVector(codes, vector, vector.Values.Length);
    }

    /// <summary>
    /// Reconstructs a vector from its PQ codes (approximation)
    /// </summary>
    public Vector Reconstruct(PQVector pqVector)
    {
        var reconstructed = new float[numSubVectors * subVectorDimensions];
        
        for (int i = 0; i < numSubVectors; i++)
        {
            var subVector = codebooks[i].ReconstructSubVector(pqVector.Codes[i]);
            Array.Copy(subVector, 0, reconstructed, i * subVectorDimensions, subVectorDimensions);
        }

        return new Vector(reconstructed, pqVector.OriginalVector.OriginalText);
    }

    /// <summary>
    /// Performs product quantized search using asymmetric distance computation
    /// </summary>
    /// <param name="query">Query vector</param>
    /// <param name="k">Number of results to return</param>
    /// <returns>List of k nearest neighbors</returns>
    public IList<Vector> Search(Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(query);
        
        if (k <= 0)
            throw new ArgumentOutOfRangeException(nameof(k), "k must be greater than 0");

        if (pqVectors.Count == 0)
            return new List<Vector>();

        // Pre-compute distances from query sub-vectors to all centroids
        var lookupTables = new float[numSubVectors][];
        for (int i = 0; i < numSubVectors; i++)
        {
            var querySubVector = ExtractSubVector(query.Values, i);
            lookupTables[i] = new float[numCentroids];
            
            for (int j = 0; j < numCentroids; j++)
            {
                if (j < codebooks[i].NumCentroids)
                {
                    lookupTables[i][j] = EuclideanDistanceSquared(querySubVector, codebooks[i].Centroids[j]);
                }
                else
                {
                    lookupTables[i][j] = float.MaxValue;
                }
            }
        }

        // Calculate approximate distances to all PQ vectors
        var distances = new List<(PQVector pqVector, float distance)>();
        foreach (var pqVector in pqVectors)
        {
            float distance = 0.0f;
            for (int i = 0; i < numSubVectors; i++)
            {
                distance += lookupTables[i][pqVector.Codes[i]];
            }
            distance = (float)Math.Sqrt(distance);
            distances.Add((pqVector, distance));
        }

        // Sort by distance and return top k original vectors
        distances.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        return distances
            .Take(k)
            .Select(d => d.pqVector.OriginalVector)
            .ToList();
    }

    /// <summary>
    /// Gets the compression ratio achieved by product quantization
    /// </summary>
    public float GetCompressionRatio()
    {
        if (pqVectors.Count == 0) return 0.0f;
        
        int originalBits = numSubVectors * subVectorDimensions * 32; // 32 bits per float
        int compressedBits = numSubVectors * 8; // 8 bits per code
        
        return (float)originalBits / compressedBits;
    }

    /// <summary>
    /// Gets memory usage statistics
    /// </summary>
    public (long originalBytes, long compressedBytes, float compressionRatio) GetMemoryStats()
    {
        if (pqVectors.Count == 0) return (0, 0, 0.0f);

        long originalBytes = pqVectors.Count * numSubVectors * subVectorDimensions * sizeof(float);
        long compressedBytes = pqVectors.Count * numSubVectors * sizeof(byte);
        long codebookBytes = numSubVectors * numCentroids * subVectorDimensions * sizeof(float);
        
        float ratio = (float)originalBytes / (compressedBytes + codebookBytes);

        return (originalBytes, compressedBytes + codebookBytes, ratio);
    }

    /// <summary>
    /// Static search method for compatibility with SearchService pattern
    /// </summary>
    public static IList<Vector> Search(VectorList vectors, Vector query, int k)
    {
        var pq = new ProductQuantization(vectors, numSubVectors: null, numCentroids: 256);
        return pq.Search(query, k);
    }
}