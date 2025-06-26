using System.Collections.Concurrent;
using Neighborly.Distance;

namespace Neighborly.Search;

/// <summary>
/// Locality Sensitive Hashing (LSH) search method using random projection hash families.
/// Maps similar input vectors to the same "bucket" with high probability using multiple hash tables.
/// </summary>
public class LSHSearch
{
    /// <summary>
    /// Represents a single hash table in the LSH structure
    /// </summary>
    private class HashTable
    {
        private readonly Dictionary<string, List<int>> buckets = new();
        private readonly RandomProjectionHashFamily hashFamily;

        public HashTable(int dimensions, int hashFunctionCount, Random random)
        {
            hashFamily = new RandomProjectionHashFamily(dimensions, hashFunctionCount, random);
        }

        public void Insert(Vector vector, int index)
        {
            var hashKey = hashFamily.Hash(vector);
            if (!buckets.ContainsKey(hashKey))
            {
                buckets[hashKey] = new List<int>();
            }
            buckets[hashKey].Add(index);
        }

        public List<int> GetCandidates(Vector query)
        {
            var hashKey = hashFamily.Hash(query);
            return buckets.TryGetValue(hashKey, out var candidates) ? candidates : new List<int>();
        }
    }

    /// <summary>
    /// Random projection hash family for LSH
    /// </summary>
    private class RandomProjectionHashFamily
    {
        private readonly float[][] projectionVectors;
        private readonly float[] biases;
        private readonly int hashFunctionCount;

        public RandomProjectionHashFamily(int dimensions, int hashFunctionCount, Random random)
        {
            this.hashFunctionCount = hashFunctionCount;
            this.projectionVectors = new float[hashFunctionCount][];
            this.biases = new float[hashFunctionCount];

            // Generate random projection vectors and biases
            for (int i = 0; i < hashFunctionCount; i++)
            {
                projectionVectors[i] = new float[dimensions];
                for (int j = 0; j < dimensions; j++)
                {
                    // Use Gaussian random numbers for projection vectors
                    projectionVectors[i][j] = (float)GenerateGaussianRandom(random);
                }
                // Random bias between 0 and bucket width (we use 1.0 as default bucket width)
                biases[i] = (float)random.NextDouble();
            }
        }

        public string Hash(Vector vector)
        {
            var hashBits = new char[hashFunctionCount];
            
            for (int i = 0; i < hashFunctionCount; i++)
            {
                float dotProduct = 0.0f;
                for (int j = 0; j < vector.Values.Length; j++)
                {
                    dotProduct += vector.Values[j] * projectionVectors[i][j];
                }
                
                // Hash bit is 1 if (dot product + bias) > 0, 0 otherwise
                hashBits[i] = (dotProduct + biases[i]) > 0 ? '1' : '0';
            }
            
            return new string(hashBits);
        }

        private static double GenerateGaussianRandom(Random random)
        {
            // Box-Muller transform to generate Gaussian random numbers
            // Use simple implementation without static locals for C# compatibility
            double u1 = random.NextDouble();
            double u2 = random.NextDouble();
            
            // Ensure u1 is not zero to avoid log(0)
            while (u1 <= double.Epsilon)
            {
                u1 = random.NextDouble();
            }
            
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }

    private readonly List<HashTable> hashTables;
    private readonly VectorList vectors;
    private readonly IDistanceCalculator distanceCalculator;
    private readonly int tableCount;
    private readonly int hashFunctionCount;
    private readonly Random random;

    /// <summary>
    /// Initializes a new LSH search instance
    /// </summary>
    /// <param name="vectors">The vector collection to index</param>
    /// <param name="tableCount">Number of hash tables (more tables = higher recall, slower performance)</param>
    /// <param name="hashFunctionCount">Number of hash functions per table (more functions = higher precision, lower recall)</param>
    /// <param name="distanceCalculator">Distance calculator for ranking results</param>
    /// <param name="seed">Random seed for reproducible results</param>
    public LSHSearch(VectorList vectors, int tableCount = 10, int hashFunctionCount = 10, 
                    IDistanceCalculator? distanceCalculator = null, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        
        this.vectors = vectors;
        this.tableCount = tableCount;
        this.hashFunctionCount = hashFunctionCount;
        this.distanceCalculator = distanceCalculator ?? EuclideanDistanceCalculator.Instance;
        this.random = new Random(seed);
        this.hashTables = new List<HashTable>();

        BuildIndex();
    }

    private void BuildIndex()
    {
        if (vectors.Count == 0) return;

        int dimensions = vectors[0].Values.Length;
        
        // Create hash tables
        for (int i = 0; i < tableCount; i++)
        {
            hashTables.Add(new HashTable(dimensions, hashFunctionCount, random));
        }

        // Insert all vectors into hash tables
        for (int vectorIndex = 0; vectorIndex < vectors.Count; vectorIndex++)
        {
            foreach (var table in hashTables)
            {
                table.Insert(vectors[vectorIndex], vectorIndex);
            }
        }
    }

    /// <summary>
    /// Performs LSH search to find approximate nearest neighbors
    /// </summary>
    /// <param name="query">Query vector</param>
    /// <param name="k">Number of neighbors to return</param>
    /// <returns>List of k nearest neighbors (approximate)</returns>
    public IList<Vector> Search(Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(query);
        
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        if (vectors.Count == 0)
        {
            return new List<Vector>();
        }

        // Collect candidate indices from all hash tables
        var candidateSet = new HashSet<int>();
        foreach (var table in hashTables)
        {
            var candidates = table.GetCandidates(query);
            foreach (var candidate in candidates)
            {
                candidateSet.Add(candidate);
            }
        }

        // If no candidates found, fall back to checking a small random sample
        if (candidateSet.Count == 0)
        {
            int sampleSize = Math.Min(vectors.Count, k * 10);
            for (int i = 0; i < sampleSize; i++)
            {
                candidateSet.Add(random.Next(vectors.Count));
            }
        }

        // Calculate distances for candidates and sort
        var candidateDistances = new List<(Vector vector, float distance)>();
        foreach (var candidateIndex in candidateSet)
        {
            var candidate = vectors[candidateIndex];
            var distance = distanceCalculator.CalculateDistance(query, candidate);
            candidateDistances.Add((candidate, distance));
        }

        // Sort by distance and take top k
        candidateDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        var results = candidateDistances
            .Take(k)
            .Select(cd => cd.vector)
            .ToList();

        return results;
    }

    /// <summary>
    /// Gets candidate vectors from LSH hash tables without calculating distances.
    /// Used for batch optimization where distances are calculated separately.
    /// </summary>
    public static IList<Vector> GetCandidates(VectorList vectors, Vector query)
    {
        if (vectors.Count == 0) return new List<Vector>();
        
        // Build LSH tables
        int dimensions = vectors[0].Values.Length;
        int tableCount = Math.Min(20, Math.Max(8, dimensions / 20));
        int projectionCount = Math.Min(12, Math.Max(4, dimensions / 50));
        var random = new Random(42); // Fixed seed for consistency
        
        var hashTables = new List<HashTable>();
        for (int i = 0; i < tableCount; i++)
        {
            hashTables.Add(new HashTable(dimensions, projectionCount, random));
        }
        
        // Add all vectors to hash tables
        for (int i = 0; i < vectors.Count; i++)
        {
            foreach (var table in hashTables)
            {
                table.Insert(vectors[i], i);
            }
        }
        
        // Get candidates from all tables
        var candidateSet = new HashSet<int>();
        foreach (var table in hashTables)
        {
            var candidates = table.GetCandidates(query);
            foreach (var candidate in candidates)
            {
                candidateSet.Add(candidate);
            }
        }
        
        // Convert indices to vectors
        return candidateSet.Select(i => vectors[i]).ToList();
    }
    
    /// <summary>
    /// Static method for backward compatibility with existing SearchService
    /// </summary>
    public static IList<Vector> Search(VectorList vectors, Vector query, int k)
    {
        if (vectors.Count == 0) return new List<Vector>();
        
        // Adjust parameters based on dimensionality and dataset size
        int dimensions = vectors[0].Values.Length;
        int tableCount = Math.Min(20, Math.Max(8, dimensions / 20)); // More tables for higher dimensions
        int hashFunctionCount = Math.Min(15, Math.Max(6, dimensions / 30)); // Fewer hash functions for higher dimensions
        
        // Create a default LSH instance for compatibility
        var lsh = new LSHSearch(vectors, tableCount, hashFunctionCount);
        return lsh.Search(query, k);
    }
}
