namespace Neighborly.Search;

/// <summary>
/// Configuration parameters for HNSW algorithm
/// </summary>
public class HNSWConfig
{
    /// <summary>
    /// Maximum number of bi-directional links for every new element during construction.
    /// Reasonable range: 2-100. Higher M leads to higher recall but more memory usage.
    /// Default: 16
    /// </summary>
    public int M { get; set; } = 16;

    /// <summary>
    /// Maximum number of bi-directional links for every new element during construction for layer 0.
    /// Should be M * 2 for optimal performance.
    /// Default: 32
    /// </summary>
    public int MaxM0 { get; set; } = 32;

    /// <summary>
    /// Size of the dynamic candidate list for index construction.
    /// Higher efConstruction leads to better recall but slower construction.
    /// Reasonable range: 100-2000. Default: 200
    /// </summary>
    public int EfConstruction { get; set; } = 200;

    /// <summary>
    /// Size of the dynamic candidate list for search.
    /// Higher ef leads to better recall but slower search.
    /// Should be >= k (number of nearest neighbors requested).
    /// Default: 200
    /// </summary>
    public int Ef { get; set; } = 200;

    /// <summary>
    /// Level generation factor. Controls the distribution of nodes across layers.
    /// Recommended value: 1 / ln(2) ≈ 1.44
    /// Default: 1.44
    /// </summary>
    public double Ml { get; set; } = 1.0 / Math.Log(2.0);

    /// <summary>
    /// Random seed for reproducible results. Set to null for non-deterministic behavior.
    /// Default: 42
    /// </summary>
    public int? Seed { get; set; } = 42;

    /// <summary>
    /// Validate the configuration parameters
    /// </summary>
    public void Validate()
    {
        if (M <= 0)
            throw new ArgumentException("M must be positive", nameof(M));
            
        if (MaxM0 <= 0)
            throw new ArgumentException("MaxM0 must be positive", nameof(MaxM0));
            
        if (EfConstruction <= 0)
            throw new ArgumentException("EfConstruction must be positive", nameof(EfConstruction));
            
        if (Ef <= 0)
            throw new ArgumentException("Ef must be positive", nameof(Ef));
            
        if (Ml <= 0)
            throw new ArgumentException("Ml must be positive", nameof(Ml));
    }

    /// <summary>
    /// Create a configuration optimized for accuracy over speed
    /// </summary>
    public static HNSWConfig HighAccuracy()
    {
        return new HNSWConfig
        {
            M = 32,
            MaxM0 = 64,
            EfConstruction = 400,
            Ef = 400
        };
    }

    /// <summary>
    /// Create a configuration optimized for speed over accuracy
    /// </summary>
    public static HNSWConfig HighSpeed()
    {
        return new HNSWConfig
        {
            M = 8,
            MaxM0 = 16,
            EfConstruction = 100,
            Ef = 100
        };
    }

    /// <summary>
    /// Create a balanced configuration for general use
    /// </summary>
    public static HNSWConfig Balanced()
    {
        return new HNSWConfig(); // Uses default values
    }
}