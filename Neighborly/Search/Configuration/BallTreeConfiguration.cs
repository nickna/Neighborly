namespace Neighborly.Search.Configuration;

/// <summary>
/// Immutable configuration for Ball-Tree operations.
/// Provides settings for construction and search behavior.
/// </summary>
public sealed record BallTreeConfiguration
{
    /// <summary>
    /// Default configuration instance with standard settings.
    /// </summary>
    public static BallTreeConfiguration Default { get; } = new();

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    public void Validate()
    {
        // Currently no validation needed for BallTree
        // This method exists for consistency and future extensibility
    }
}
