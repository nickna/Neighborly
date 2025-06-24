using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography; // For random number generation if needed later for secure seeds

namespace Neighborly.Search;

/// <summary>
/// Locality Sensitive Hashing (LSH) search method using Random Projection for Cosine Similarity.
/// </summary>
public class LSHSearch
{
    private readonly LSHConfig _config;
    private readonly Random _random;

    // Each element in the outer list represents a hash table.
    // Each inner list contains 'k' random projection vectors for that table.
    private List<List<float[]>> _projectionVectors;

    // L hash tables. Key is the k-bit hash code (represented as an int or long).
    // Value is a list of vector IDs that hashed to this bucket.
    // Using List<Guid> to store vector IDs.
    private List<Dictionary<int, List<Guid>>> _hashTables;

    private VectorList? _indexedVectors; // Keep a reference to the indexed vectors for distance calculations

    public LSHSearch(LSHConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        _config = config;

        _random = _config.Seed.HasValue ? new Random(_config.Seed.Value) : new Random();
        _projectionVectors = new List<List<float[]>>(_config.NumberOfHashTables);
        _hashTables = new List<Dictionary<int, List<Guid>>>(_config.NumberOfHashTables);

        InitializeProjections();
    }

    private void InitializeProjections()
    {
        for (int i = 0; i < _config.NumberOfHashTables; i++)
        {
            var tableProjections = new List<float[]>(_config.HashesPerTable);
            for (int j = 0; j < _config.HashesPerTable; j++)
            {
                var projection = new float[_config.VectorDimensions];
                for (int d = 0; d < _config.VectorDimensions; d++)
                {
                    // Generate components from a standard normal distribution (mean 0, stddev 1)
                    // Box-Muller transform or other methods can be used. Here's a simple approximation:
                    double u1 = 1.0 - _random.NextDouble(); //uniform(0,1] random doubles
                    double u2 = 1.0 - _random.NextDouble();
                    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                    projection[d] = (float)randStdNormal;
                }
                // Normalize the projection vector (optional but good practice for cosine LSH)
                Normalize(projection);
                tableProjections.Add(projection);
            }
            _projectionVectors.Add(tableProjections);
            _hashTables.Add(new Dictionary<int, List<Guid>>());
        }
    }

    private static void Normalize(float[] vector)
    {
        float norm = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            norm += vector[i] * vector[i];
        }
        norm = (float)Math.Sqrt(norm);
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
    }

    /// <summary>
    /// Builds the LSH index from the given list of vectors.
    /// </summary>
    /// <param name="vectors">The list of vectors to index.</param>
    public void Build(VectorList vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        _indexedVectors = vectors;

        // Clear existing hash tables
        foreach (var table in _hashTables)
        {
            table.Clear();
        }

        if (vectors.Count == 0)
            return;

        if (vectors[0].Values.Length != _config.VectorDimensions)
        {
            throw new ArgumentException($"Vector dimension mismatch. Config expects {_config.VectorDimensions}, but vectors have {vectors[0].Values.Length}. Reconfigure LSH or provide matching vectors.");
        }

        for (int i = 0; i < vectors.Count; i++)
        {
            Vector vector = vectors[i];
            if (vector == null) continue;

            for (int tableIndex = 0; tableIndex < _config.NumberOfHashTables; tableIndex++)
            {
                int hashCode = ComputeHashCode(vector, tableIndex);
                if (!_hashTables[tableIndex].TryGetValue(hashCode, out var bucket))
                {
                    bucket = new List<Guid>();
                    _hashTables[tableIndex][hashCode] = bucket;
                }
                bucket.Add(vector.Id);
            }
        }
    }

    private int ComputeHashCode(Vector vector, int tableIndex)
    {
        int hashCode = 0;
        var projectionsForTable = _projectionVectors[tableIndex];

        for (int hashIndex = 0; hashIndex < _config.HashesPerTable; hashIndex++)
        {
            float dotProduct = 0;
            var projection = projectionsForTable[hashIndex];
            for (int d = 0; d < _config.VectorDimensions; d++)
            {
                dotProduct += vector.Values[d] * projection[d];
            }

            if (dotProduct >= 0)
            {
                hashCode |= (1 << hashIndex);
            }
        }
        return hashCode;
    }

    /// <summary>
    /// Searches for the k nearest neighbors to the query vector.
    /// </summary>
    /// <param name="query">The query vector.</param>
    /// <param name="k">The number of nearest neighbors to return.</param>
    /// <returns>A list of the k nearest neighbors.</returns>
    public IList<Vector> Search(Vector query, int k)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_indexedVectors == null)
        {
            // Or throw new InvalidOperationException("LSH index has not been built.");
            return new List<Vector>();
        }

        if (query.Values.Length != _config.VectorDimensions)
        {
            throw new ArgumentException($"Query vector dimension mismatch. Config expects {_config.VectorDimensions}, but query has {query.Values.Length}.");
        }

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "Number of neighbors must be greater than 0");
        }

        var candidateIds = new HashSet<Guid>();

        for (int tableIndex = 0; tableIndex < _config.NumberOfHashTables; tableIndex++)
        {
            int queryHashCode = ComputeHashCode(query, tableIndex);
            if (_hashTables[tableIndex].TryGetValue(queryHashCode, out var bucket))
            {
                foreach (var id in bucket)
                {
                    candidateIds.Add(id);
                }
            }
        }

        if (candidateIds.Count == 0)
        {
            return new List<Vector>();
        }

        // Retrieve full candidate vectors and calculate exact distances
        var candidatesWithDistances = new List<(Vector vector, float distance)>();
        foreach (var id in candidateIds)
        {
            var vector = _indexedVectors.GetById(id); // Assuming VectorList has GetById
            if (vector != null)
            {
                // For Random Projection LSH, cosine similarity is the target.
                // Distance = 1 - CosineSimilarity. Smaller is better.
                var cosineCalculator = new Distance.CosineSimilarityCalculator();
                // CalculateDistance on CosineSimilarityCalculator returns the similarity.
                float similarity = cosineCalculator.CalculateDistance(query, vector);
                candidatesWithDistances.Add((vector, 1 - similarity)); // Convert similarity to distance
            }
        }

        // Sort by distance (ascending) and take top k
        return candidatesWithDistances
            .OrderBy(c => c.distance)
            .Take(k)
            .Select(c => c.vector)
            .ToList();
    }

    // Placeholder for future load/save functionality
    public void Save(BinaryWriter writer)
    {
        // TODO: Implement saving of LSH configuration and hash tables/projections
        // writer.Write(_config.NumberOfHashTables);
        // writer.Write(_config.HashesPerTable);
        // writer.Write(_config.VectorDimensions);
        // ... save projection vectors ...
        // ... save hash tables ...
        throw new NotImplementedException();
    }

    public void Load(BinaryReader reader, int vectorDimensions)
    {
        // TODO: Implement loading of LSH configuration and hash tables/projections
        // _config = new LSHConfig(vectorDimensions, reader.ReadInt32(), reader.ReadInt32());
        // ... load projection vectors ...
        // ... load hash tables ...
        // Note: Build() would typically be called after loading vectors into VectorList
        throw new NotImplementedException();
    }
}
