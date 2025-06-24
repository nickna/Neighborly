using System;

namespace Neighborly.Search;

/// <summary>
/// Configuration for the Locality Sensitive Hashing (LSH) search algorithm.
/// </summary>
public class LSHConfig
{
    /// <summary>
    /// Gets or sets the number of hash tables (L).
    /// More tables increase recall but also query time and memory.
    /// </summary>
    public int NumberOfHashTables { get; set; }

    /// <summary>
    /// Gets or sets the number of hash functions per table (k).
    /// This determines the number of bits in the resulting hash key for each table.
    /// More hashes per table increase precision but can decrease recall for a single table.
    /// </summary>
    public int HashesPerTable { get; set; }

    /// <summary>
    /// Gets or sets the dimensionality of the vectors being indexed.
    /// </summary>
    public int VectorDimensions { get; private set; }

    /// <summary>
    /// Gets or sets the optional seed for random number generation.
    /// Using a seed makes index construction deterministic.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSHConfig"/> class.
    /// </summary>
    /// <param name="vectorDimensions">The dimensionality of the vectors.</param>
    /// <param name="numberOfHashTables">Default is 10.</param>
    /// <param name="hashesPerTable">Default is 8.</param>
    /// <param name="seed">Optional seed for randomness.</param>
    public LSHConfig(int vectorDimensions, int numberOfHashTables = 10, int hashesPerTable = 8, int? seed = null)
    {
        if (vectorDimensions <= 0)
            throw new ArgumentOutOfRangeException(nameof(vectorDimensions), "Vector dimensions must be positive.");
        if (numberOfHashTables <= 0)
            throw new ArgumentOutOfRangeException(nameof(numberOfHashTables), "Number of hash tables must be positive.");
        if (hashesPerTable <= 0)
            throw new ArgumentOutOfRangeException(nameof(hashesPerTable), "Hashes per table must be positive.");

        VectorDimensions = vectorDimensions;
        NumberOfHashTables = numberOfHashTables;
        HashesPerTable = hashesPerTable;
        Seed = seed;
    }

    /// <summary>
    /// Validates the configuration parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (VectorDimensions <= 0)
            throw new InvalidOperationException("Vector dimensions must be positive.");
        if (NumberOfHashTables <= 0)
            throw new InvalidOperationException("Number of hash tables must be positive.");
        if (HashesPerTable <= 0)
            throw new InvalidOperationException("Hashes per table must be positive.");
    }
}
