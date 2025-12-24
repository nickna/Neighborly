namespace Neighborly.Search.Configuration;

/// <summary>
/// Immutable configuration for SearchService.
/// Provides settings for index selection, algorithm parameters, and performance tuning.
/// </summary>
public sealed record SearchServiceConfiguration
{
    /// <summary>
    /// Gets or sets whether to use immutable index structures for lock-free reads.
    /// When enabled, search operations on KDTree and BallTree do not require locks.
    /// Default: false.
    /// </summary>
    public bool UseImmutableIndexes { get; init; } = false;

    /// <summary>
    /// Configuration for KD-Tree algorithm.
    /// </summary>
    public KDTreeConfiguration KDTree { get; init; } = KDTreeConfiguration.Default;

    /// <summary>
    /// Configuration for Ball-Tree algorithm.
    /// </summary>
    public BallTreeConfiguration BallTree { get; init; } = BallTreeConfiguration.Default;

    /// <summary>
    /// Configuration for HNSW algorithm.
    /// </summary>
    public HNSWConfig HNSW { get; init; } = new HNSWConfig();

    /// <summary>
    /// Default configuration instance with standard settings.
    /// </summary>
    public static SearchServiceConfiguration Default { get; } = new();

    /// <summary>
    /// Validates all configuration settings.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when any configuration is invalid.</exception>
    public void Validate()
    {
        KDTree.Validate();
        BallTree.Validate();
        HNSW.Validate();
    }
}
