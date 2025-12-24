namespace Neighborly;

/// <summary>
/// Configuration options for the background index service.
/// Controls how and when the database rebuilds search indexes in response to modifications.
/// </summary>
/// <example>
/// <code>
/// // Use aggressive rebuilding for real-time applications
/// var options = BackgroundIndexServiceOptions.Aggressive();
/// var db = new VectorDatabase(logger, instrumentation, options);
///
/// // Custom configuration
/// var options = new BackgroundIndexServiceOptions
/// {
///     RebuildDelay = TimeSpan.FromSeconds(10),
///     CheckInterval = TimeSpan.FromSeconds(2)
/// };
/// var db = new VectorDatabase(logger, instrumentation, options);
/// </code>
/// </example>
public class BackgroundIndexServiceOptions
{
    /// <summary>
    /// Time to wait after the last modification before triggering an index rebuild.
    /// This batches multiple rapid changes into a single rebuild operation.
    /// Valid range: 100ms to 5 minutes.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan RebuildDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How frequently the service checks if a rebuild is needed.
    /// Lower values provide faster response but slightly higher CPU usage.
    /// Valid range: 100ms to 1 minute.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum time to wait for graceful shutdown of the indexing service.
    /// If the service doesn't stop within this timeout, disposal will proceed anyway.
    /// Valid range: 1 second to 1 minute.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Master switch to enable or disable background indexing entirely.
    /// When false, indexes must be rebuilt manually via RebuildSearchIndexesAsync().
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Automatically disable background indexing on mobile platforms (iOS/Android).
    /// Mobile platforms have limited background processing capabilities and battery constraints.
    /// When true and running on iOS/Android, background indexing is disabled regardless of Enabled property.
    /// Default: true
    /// </summary>
    public bool AutoDisableOnMobile { get; set; } = true;

    /// <summary>
    /// Validates the configuration options and throws ArgumentException if invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is outside valid range.</exception>
    public void Validate()
    {
        if (RebuildDelay < TimeSpan.FromMilliseconds(100))
            throw new ArgumentOutOfRangeException(
                nameof(RebuildDelay),
                RebuildDelay,
                "RebuildDelay must be at least 100ms");

        if (RebuildDelay > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(
                nameof(RebuildDelay),
                RebuildDelay,
                "RebuildDelay must not exceed 5 minutes");

        if (CheckInterval < TimeSpan.FromMilliseconds(100))
            throw new ArgumentOutOfRangeException(
                nameof(CheckInterval),
                CheckInterval,
                "CheckInterval must be at least 100ms");

        if (CheckInterval > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(
                nameof(CheckInterval),
                CheckInterval,
                "CheckInterval must not exceed 1 minute");

        if (ShutdownTimeout < TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(
                nameof(ShutdownTimeout),
                ShutdownTimeout,
                "ShutdownTimeout must be at least 1 second");

        if (ShutdownTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(
                nameof(ShutdownTimeout),
                ShutdownTimeout,
                "ShutdownTimeout must not exceed 1 minute");
    }

    /// <summary>
    /// Creates the default configuration matching current behavior.
    /// </summary>
    /// <returns>Options with 5-second delays and mobile auto-disable enabled</returns>
    public static BackgroundIndexServiceOptions Default()
    {
        return new BackgroundIndexServiceOptions();
    }

    /// <summary>
    /// Creates an aggressive configuration for fast-changing databases.
    /// Rebuilds indexes quickly after changes for minimal search staleness.
    /// </summary>
    /// <returns>Options with 1-second rebuild delay and check interval</returns>
    public static BackgroundIndexServiceOptions Aggressive()
    {
        return new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromSeconds(1),
            CheckInterval = TimeSpan.FromSeconds(1),
            ShutdownTimeout = TimeSpan.FromSeconds(3)
        };
    }

    /// <summary>
    /// Creates a conservative configuration for slower, batch-oriented workloads.
    /// Waits longer before rebuilding to batch more changes together.
    /// </summary>
    /// <returns>Options with 30-second rebuild delay</returns>
    public static BackgroundIndexServiceOptions Conservative()
    {
        return new BackgroundIndexServiceOptions
        {
            RebuildDelay = TimeSpan.FromSeconds(30),
            CheckInterval = TimeSpan.FromSeconds(10),
            ShutdownTimeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Disables automatic background indexing entirely.
    /// Indexes must be rebuilt manually by calling RebuildSearchIndexesAsync().
    /// </summary>
    /// <returns>Options with background indexing disabled</returns>
    public static BackgroundIndexServiceOptions Disabled()
    {
        return new BackgroundIndexServiceOptions
        {
            Enabled = false,
            AutoDisableOnMobile = false
        };
    }

    /// <summary>
    /// Creates a mobile-optimized configuration.
    /// Explicitly disables background processing for battery conservation.
    /// </summary>
    /// <returns>Options optimized for mobile platforms</returns>
    public static BackgroundIndexServiceOptions Mobile()
    {
        return new BackgroundIndexServiceOptions
        {
            Enabled = false,
            AutoDisableOnMobile = true
        };
    }
}
