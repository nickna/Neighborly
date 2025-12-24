namespace Neighborly;

/// <summary>
/// Encapsulates file growth strategy for dynamic capacity management.
/// Uses exponential growth (1.5x) up to 1 GB, then fixed increments (256 MB).
/// Thread-safe and stateless.
/// </summary>
internal class FileGrowthStrategy
{
    private readonly long _initialCapacity;
    private readonly double _growthFactor;
    private readonly long _exponentialThreshold;
    private readonly long _fixedIncrement;
    private readonly double _capacityThreshold;

    // Constants for default strategies
    private const long DefaultInitialIndexCapacity = 1 * 1024 * 1024;      // 1 MB
    private const long DefaultInitialDataCapacity = 10 * 1024 * 1024;      // 10 MB
    private const double DefaultGrowthFactor = 1.5;
    private const long DefaultExponentialThreshold = 1L * 1024 * 1024 * 1024; // 1 GB
    private const long DefaultFixedIncrement = 256L * 1024 * 1024;         // 256 MB
    private const double DefaultCapacityThreshold = 0.90;                  // 90%

    /// <summary>
    /// Gets the initial capacity for this growth strategy.
    /// </summary>
    public long InitialCapacity => _initialCapacity;

    /// <summary>
    /// Gets the capacity threshold (0.0 to 1.0) at which growth is triggered.
    /// </summary>
    public double CapacityThreshold => _capacityThreshold;

    /// <summary>
    /// Creates a growth strategy optimized for index files (smaller initial size).
    /// </summary>
    /// <returns>A FileGrowthStrategy configured for index files.</returns>
    public static FileGrowthStrategy ForIndexFile()
    {
        return new FileGrowthStrategy(
            DefaultInitialIndexCapacity,
            DefaultGrowthFactor,
            DefaultExponentialThreshold,
            DefaultFixedIncrement,
            DefaultCapacityThreshold);
    }

    /// <summary>
    /// Creates a growth strategy optimized for data files (larger initial size).
    /// </summary>
    /// <returns>A FileGrowthStrategy configured for data files.</returns>
    public static FileGrowthStrategy ForDataFile()
    {
        return new FileGrowthStrategy(
            DefaultInitialDataCapacity,
            DefaultGrowthFactor,
            DefaultExponentialThreshold,
            DefaultFixedIncrement,
            DefaultCapacityThreshold);
    }

    /// <summary>
    /// Creates a custom growth strategy with specified parameters.
    /// </summary>
    /// <param name="initialCapacity">Initial file capacity in bytes.</param>
    /// <param name="growthFactor">Multiplicative growth factor for exponential phase (e.g., 1.5 for 50% growth).</param>
    /// <param name="exponentialThreshold">Threshold in bytes beyond which growth switches to fixed increments.</param>
    /// <param name="fixedIncrement">Fixed increment size in bytes for large files.</param>
    /// <param name="capacityThreshold">Threshold (0.0 to 1.0) at which to trigger growth.</param>
    public FileGrowthStrategy(
        long initialCapacity,
        double growthFactor = DefaultGrowthFactor,
        long exponentialThreshold = DefaultExponentialThreshold,
        long fixedIncrement = DefaultFixedIncrement,
        double capacityThreshold = DefaultCapacityThreshold)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity must be positive");
        if (growthFactor <= 1.0)
            throw new ArgumentOutOfRangeException(nameof(growthFactor), "Growth factor must be greater than 1.0");
        if (exponentialThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(exponentialThreshold), "Exponential threshold must be positive");
        if (fixedIncrement <= 0)
            throw new ArgumentOutOfRangeException(nameof(fixedIncrement), "Fixed increment must be positive");
        if (capacityThreshold <= 0.0 || capacityThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(capacityThreshold), "Capacity threshold must be between 0.0 and 1.0");

        _initialCapacity = initialCapacity;
        _growthFactor = growthFactor;
        _exponentialThreshold = exponentialThreshold;
        _fixedIncrement = fixedIncrement;
        _capacityThreshold = capacityThreshold;
    }

    /// <summary>
    /// Determines if the file should grow based on current usage.
    /// Thread-safe and stateless.
    /// </summary>
    /// <param name="currentCapacity">Current file capacity in bytes.</param>
    /// <param name="usedSpace">Currently used space in bytes.</param>
    /// <returns>True if growth is recommended, false otherwise.</returns>
    public bool ShouldGrow(long currentCapacity, long usedSpace)
    {
        if (currentCapacity == 0)
            return true;

        if (usedSpace < 0)
            throw new ArgumentOutOfRangeException(nameof(usedSpace), "Used space cannot be negative");

        double usageRatio = (double)usedSpace / currentCapacity;
        return usageRatio >= _capacityThreshold;
    }

    /// <summary>
    /// Calculates the new capacity based on the growth strategy.
    /// Ensures the result is always >= requiredSpace.
    /// Thread-safe and stateless.
    /// </summary>
    /// <param name="currentCapacity">Current file capacity in bytes.</param>
    /// <param name="requiredSpace">Minimum required space in bytes (e.g., for a specific operation).</param>
    /// <returns>The recommended new capacity in bytes.</returns>
    public long CalculateNewCapacity(long currentCapacity, long requiredSpace)
    {
        if (currentCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(currentCapacity), "Current capacity cannot be negative");
        if (requiredSpace < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredSpace), "Required space cannot be negative");

        long newCapacity;

        if (currentCapacity < _exponentialThreshold)
        {
            // Exponential growth phase (e.g., 1.5x)
            newCapacity = (long)(currentCapacity * _growthFactor);
        }
        else
        {
            // Fixed increment phase (e.g., +256 MB)
            newCapacity = currentCapacity + _fixedIncrement;
        }

        // Ensure we meet the required space constraint
        // Keep growing until we have enough capacity
        while (newCapacity < requiredSpace)
        {
            if (newCapacity < _exponentialThreshold)
            {
                newCapacity = (long)(newCapacity * _growthFactor);
            }
            else
            {
                newCapacity += _fixedIncrement;
            }
        }

        return newCapacity;
    }

    /// <summary>
    /// Returns the minimum capacity needed for the given space requirement.
    /// </summary>
    /// <param name="requiredSpace">Required space in bytes.</param>
    /// <returns>The minimum recommended capacity.</returns>
    public long GetMinimumCapacityFor(long requiredSpace)
    {
        if (requiredSpace < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredSpace), "Required space cannot be negative");

        return Math.Max(_initialCapacity, requiredSpace);
    }
}
