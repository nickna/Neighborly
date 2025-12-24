namespace Neighborly.Search.Configuration;

/// <summary>
/// Immutable configuration for KD-Tree parallel processing.
/// Provides settings for parallel construction and search operations.
/// </summary>
public sealed record KDTreeConfiguration
{
    /// <summary>
    /// Minimum dataset size to enable parallel tree construction.
    /// Default: 1000 vectors.
    /// </summary>
    public int ParallelConstructionThreshold { get; init; } = 1000;

    /// <summary>
    /// Minimum subtree size to continue parallel processing during construction.
    /// Default: 100 vectors.
    /// </summary>
    public int MinParallelSubtreeSize { get; init; } = 100;

    /// <summary>
    /// Maximum depth for parallel search operations (k-NN and range).
    /// Beyond this depth, operations become sequential to avoid task overhead.
    /// Default: 4 levels.
    /// </summary>
    public int MaxParallelSearchDepth { get; init; } = 4;

    /// <summary>
    /// Enable or disable parallel tree construction.
    /// Default: true.
    /// </summary>
    public bool EnableParallelConstruction { get; init; } = true;

    /// <summary>
    /// Enable or disable parallel search operations.
    /// Default: true.
    /// </summary>
    public bool EnableParallelSearch { get; init; } = true;

    /// <summary>
    /// Default configuration instance with standard settings.
    /// </summary>
    public static KDTreeConfiguration Default { get; } = new();

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when configuration values are invalid.</exception>
    public void Validate()
    {
        if (ParallelConstructionThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ParallelConstructionThreshold),
                "Parallel construction threshold must be non-negative");
        }

        if (MinParallelSubtreeSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinParallelSubtreeSize),
                "Minimum parallel subtree size must be non-negative");
        }

        if (MaxParallelSearchDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxParallelSearchDepth),
                "Maximum parallel search depth must be non-negative");
        }
    }
}
