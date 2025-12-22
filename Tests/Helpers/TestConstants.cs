namespace Neighborly.Tests.Helpers;

/// <summary>
/// Centralized test constants to eliminate magic numbers and improve test maintainability.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Timeout and delay values for async operations in tests.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>Short delay for fast operations (5ms).</summary>
        public const int ShortDelayMs = 5;

        /// <summary>Standard operation delay (10ms).</summary>
        public const int OperationDelayMs = 10;

        /// <summary>Delay for enumeration operations (50ms).</summary>
        public const int EnumerationDelayMs = 50;

        /// <summary>Sleep duration during enumeration tests (100ms).</summary>
        public const int EnumerationSleepMs = 100;

        /// <summary>Duration for mixed concurrent operations (2000ms).</summary>
        public const int MixedOperationsDurationMs = 2000;

        /// <summary>Timeout for task completion in seconds (5s).</summary>
        public const int TaskCompletionTimeoutSeconds = 5;
    }

    /// <summary>
    /// Common vector dimensions used in tests.
    /// </summary>
    public static class Dimensions
    {
        /// <summary>Small dimension for quick tests (32).</summary>
        public const int Small = 32;

        /// <summary>Medium dimension (64).</summary>
        public const int Medium = 64;

        /// <summary>Standard dimension for most tests (128).</summary>
        public const int Standard = 128;

        /// <summary>Large dimension (256).</summary>
        public const int Large = 256;

        /// <summary>Extra large dimension (512).</summary>
        public const int XLarge = 512;

        /// <summary>Common embedding dimension (768) - used by many models.</summary>
        public const int Embedding768 = 768;

        /// <summary>OpenAI embedding dimension (1536).</summary>
        public const int OpenAIEmbedding = 1536;

        /// <summary>Large compression test dimension (4096).</summary>
        public const int Compression4K = 4096;
    }

    /// <summary>
    /// Operation and loop counts for tests.
    /// </summary>
    public static class Counts
    {
        /// <summary>Small count for quick tests (10).</summary>
        public const int Small = 10;

        /// <summary>Medium-small count (20).</summary>
        public const int MediumSmall = 20;

        /// <summary>Small-medium count (50).</summary>
        public const int SmallMedium = 50;

        /// <summary>Default count for standard tests (100).</summary>
        public const int Default = 100;

        /// <summary>Medium-large count (200).</summary>
        public const int MediumLarge = 200;

        /// <summary>Medium count (500).</summary>
        public const int Medium = 500;

        /// <summary>Large count for comprehensive tests (1000).</summary>
        public const int Large = 1000;

        /// <summary>Very large count (1500).</summary>
        public const int VeryLarge = 1500;

        /// <summary>Extra large count (2000).</summary>
        public const int XLarge = 2000;

        /// <summary>Count for 5000 operations.</summary>
        public const int FiveThousand = 5000;

        /// <summary>Stress test count (10000).</summary>
        public const int Stress = 10000;

        /// <summary>Large stress test count (30000).</summary>
        public const int LargeStress = 30000;
    }

    /// <summary>
    /// Thread and parallelism configuration.
    /// </summary>
    public static class Parallelism
    {
        /// <summary>Single thread (1).</summary>
        public const int SingleThread = 1;

        /// <summary>Two threads/writers (2).</summary>
        public const int TwoThreads = 2;

        /// <summary>Few threads (3).</summary>
        public const int FewThreads = 3;

        /// <summary>Default thread count (5).</summary>
        public const int DefaultThreads = 5;

        /// <summary>Stress test thread count (8).</summary>
        public const int StressThreads = 8;

        /// <summary>Many threads for heavy concurrency tests (10).</summary>
        public const int ManyThreads = 10;
    }

    /// <summary>
    /// Search parameters including k values and thresholds.
    /// </summary>
    public static class Search
    {
        /// <summary>Small k value for nearest neighbor search (2).</summary>
        public const int TinyK = 2;

        /// <summary>Small k value for nearest neighbor search (3).</summary>
        public const int SmallK = 3;

        /// <summary>Default k value for nearest neighbor search (5).</summary>
        public const int DefaultK = 5;

        /// <summary>Large k value for broader search (10).</summary>
        public const int LargeK = 10;

        /// <summary>Benchmark k value (20).</summary>
        public const int BenchmarkK = 20;

        /// <summary>Stress test k value (50).</summary>
        public const int StressK = 50;

        /// <summary>Very small distance threshold.</summary>
        public const float TinyThreshold = 0.3f;

        /// <summary>Small distance/similarity threshold.</summary>
        public const float SmallThreshold = 0.8f;

        /// <summary>Standard distance threshold (1.0).</summary>
        public const float StandardThreshold = 1.0f;

        /// <summary>Large distance threshold (2.0).</summary>
        public const float LargeThreshold = 2.0f;

        /// <summary>Very large distance threshold (10.0).</summary>
        public const float VeryLargeThreshold = 10.0f;
    }

    /// <summary>
    /// Float comparison tolerances.
    /// </summary>
    public static class Tolerances
    {
        /// <summary>Default tolerance for float comparisons (1e-5).</summary>
        public const float Default = 1e-5f;

        /// <summary>Strict tolerance for precise comparisons (1e-6).</summary>
        public const float Strict = 1e-6f;
    }

    /// <summary>
    /// HNSW algorithm configuration values.
    /// </summary>
    public static class HnswConfig
    {
        /// <summary>Default M value (max connections per node) - 16.</summary>
        public const int DefaultM = 16;

        /// <summary>Custom M value for tests - 32.</summary>
        public const int CustomM = 32;

        /// <summary>Custom MaxM0 value for tests - 64.</summary>
        public const int CustomMaxM0 = 64;

        /// <summary>Default EfConstruction value - 200.</summary>
        public const int DefaultEfConstruction = 200;

        /// <summary>High EfConstruction for accuracy tests - 400.</summary>
        public const int HighEfConstruction = 400;
    }

    /// <summary>
    /// Product quantization configuration.
    /// </summary>
    public static class ProductQuantization
    {
        /// <summary>Small sub-vector count (4).</summary>
        public const int SmallSubVectors = 4;

        /// <summary>Default sub-vector count (8).</summary>
        public const int DefaultSubVectors = 8;

        /// <summary>Small centroid count (8).</summary>
        public const int SmallCentroids = 8;

        /// <summary>Default centroid count (16).</summary>
        public const int DefaultCentroids = 16;

        /// <summary>Small dimension for PQ tests (16).</summary>
        public const int SmallDimension = 16;
    }

    /// <summary>
    /// Compression test thresholds.
    /// </summary>
    public static class Compression
    {
        /// <summary>Minimum compression ratio for 4K vectors.</summary>
        public const float MinRatio4K = 1.2f;

        /// <summary>Minimum compression ratio for 512/768 vectors.</summary>
        public const float MinRatio512_768 = 1.18f;
    }

    /// <summary>
    /// Benchmark performance thresholds.
    /// </summary>
    public static class Benchmarks
    {
        /// <summary>Maximum search time in milliseconds (100ms).</summary>
        public const int MaxSearchTimeMs = 100;

        /// <summary>Maximum search time for large datasets (1000ms).</summary>
        public const int MaxSearchTimeLargeMs = 1000;

        /// <summary>Maximum build time in milliseconds (30000ms).</summary>
        public const int MaxBuildTimeMs = 30000;

        /// <summary>Minimum recall threshold (0.8).</summary>
        public const double MinRecall = 0.8;
    }

    /// <summary>
    /// Reproducible random seed for deterministic tests.
    /// </summary>
    public const int RandomSeed = 42;

    /// <summary>
    /// Memory-mapped list configuration.
    /// </summary>
    public static class MemoryMapped
    {
        /// <summary>Default capacity for memory-mapped list tests (1000).</summary>
        public const int DefaultCapacity = 1000;

        /// <summary>Pre-population count for concurrency tests (50).</summary>
        public const int PrePopulationCount = 50;

        /// <summary>Enumeration break point (10).</summary>
        public const int EnumerationBreakCount = 10;
    }

    /// <summary>
    /// Concurrency test configuration.
    /// </summary>
    public static class Concurrency
    {
        /// <summary>Maximum retry attempts (10).</summary>
        public const int MaxRetries = 10;

        /// <summary>Small iteration limit (3).</summary>
        public const int SmallIterationLimit = 3;
    }
}
