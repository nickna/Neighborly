using Microsoft.Extensions.Logging;
using Neighborly.ETL;
using Neighborly.Search;
using Neighborly.Distance;
using static Neighborly.Search.SearchService;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using static System.Net.Mime.MediaTypeNames;

namespace Neighborly;

/// <summary>
/// Represents a database for storing and searching vectors.
/// </summary>
public partial class VectorDatabase : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// The version of the database file format that this class writes.
    /// </summary>
    private const int s_currentFileVersion = 1;

    private readonly ILogger<VectorDatabase> _logger = Logging.LoggerFactory.CreateLogger<VectorDatabase>();
    private readonly Instrumentation _instrumentation;
    /// <summary>
    /// The unique identifier of the database for telemetry purposes.
    /// </summary>
    private readonly Guid _id = Guid.NewGuid();
    private readonly IEnumerable<KeyValuePair<string, object?>> _defaultTags;
    private readonly VectorList _vectors = new();
    private readonly System.Diagnostics.Metrics.Counter<long> _indexRebuildCounter;
    public VectorList Vectors => _vectors;

    /// <summary>
    /// Thread-safe method to add a vector to the database.
    /// </summary>
    /// <param name="vector">The vector to add</param>
    public void AddVector(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        
        _rwLock.EnterWriteLock();
        try
        {
            _vectors.Add(vector);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Thread-safe method to update a vector in the database.
    /// </summary>
    /// <param name="id">The ID of the vector to update</param>
    /// <param name="vector">The new vector data</param>
    /// <returns>True if the vector was updated, false if not found</returns>
    public bool UpdateVector(Guid id, Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        
        _rwLock.EnterWriteLock();
        try
        {
            return _vectors.Update(id, vector);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Thread-safe method to remove a vector from the database.
    /// </summary>
    /// <param name="vector">The vector to remove</param>
    /// <returns>True if the vector was removed, false if not found</returns>
    public bool RemoveVector(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        
        _rwLock.EnterWriteLock();
        try
        {
            return _vectors.Remove(vector);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Thread-safe method to get a vector by ID.
    /// </summary>
    /// <param name="id">The ID of the vector to retrieve</param>
    /// <returns>The vector if found, null otherwise</returns>
    public Vector? GetVector(Guid id)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _vectors.GetById(id);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }
    private Search.SearchService _searchService = null!;
    private ReaderWriterLockSlim _rwLock = new();

    // Unified cancellation token for all shutdown coordination
    private readonly CancellationTokenSource _shutdownCts = new();

    // Background indexing task (replaces Thread)
    private Task? _indexingTask;

    // PeriodicTimer for async-native timed loops
    private PeriodicTimer? _indexingTimer;


    /// <summary>
    /// Last time the database was modified. This is updated when a vector is added or removed.
    /// </summary>
    private DateTime _lastModification = DateTime.UtcNow;

    /// <summary>
    /// The time threshold in seconds for rebuilding the search indexes and VectorTags after a database change was detected.
    /// </summary>
    private const int timeThresholdSeconds = 5;

    /// <summary>
    /// Gets the number of vectors in the database.
    /// </summary>
    public int Count => _vectors.Count;

    /// <summary>
    /// Gets a value indicating whether the database is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Indicates whether the database has been modified since the last save.
    /// </summary>
    private bool _hasUnsavedChanges = false;

    /// <summary>
    /// Indicates whether the database has changed since the last indexing, and it needs to be rebuilt.
    /// </summary>
    private bool _hasOutdatedIndex = false;

    private bool _disposedValue;

    // Tracks if async disposal has started (for thread-safe single-entry)
    private int _asyncDisposeStarted;

    /// <summary>
    /// Gets a value indicating whether the database has been modified since the last save.
    /// </summary>
    public bool HasUnsavedChanges { get { return _hasUnsavedChanges; } }

    private void VectorList_Modified(object? sender, EventArgs e)
    {
        _lastModification = DateTime.UtcNow;
        _hasUnsavedChanges = true;
        _hasOutdatedIndex = true;
    }

    /// <summary>
    /// Passes in details about how to generate embeddings.
    /// </summary>
    /// <seealso cref="EmbeddingGenerationInfo"/>
    public void SetEmbeddingGenerationInfo(EmbeddingGenerationInfo embeddingGeneratorInfo)
    {
        _searchService.EmbeddingGenerator = new EmbeddingGenerator(embeddingGeneratorInfo);
    }

    /// <summary>
    /// Generates a Vector class from text.
    /// </summary>
    /// <param name="originalText"></param>
    /// <returns></returns>
    public Vector GenerateVector(string originalText)
    {
        float[] embedding = _searchService.EmbeddingGenerator.GenerateEmbedding(originalText);
        return new Vector(embedding, originalText);
    }

    /// <summary>
    /// Checks if the given search method can use lock-free immutable indexes.
    /// </summary>
    private static bool CanUseLockFreeSearch(SearchAlgorithm searchMethod)
    {
        return UseImmutableIndexes &&
               (searchMethod == SearchAlgorithm.KDTree || searchMethod == SearchAlgorithm.BallTree);
    }

    /// <summary>
    /// Searches for a specified text in the database and returns the k nearest neighbors.
    /// This text is first converted into an embedding using the EmbeddingGenerator.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="k">Proximity</param>
    /// <param name="searchMethod">Search algorithm</param>
    /// <param name="similarityThreshold"></param>
    /// <returns>Vectors that match the search results</returns>
    /// <seealso cref="EmbeddingGenerator"/>
    public IList<Vector> Search(string text, int k, SearchAlgorithm searchMethod = SearchAlgorithm.KDTree, float similarityThreshold = 0.5f)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.k", k)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.Search(text, k, searchMethod, similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.Search(text, k, searchMethod, similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Searches for a specified vector in the database and returns the k nearest neighbors.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="k"></param>
    /// <param name="searchMethod">Search algorithm</param>
    /// <param name="similarityThreshold"></param>
    /// <returns></returns>
    public IList<Vector> Search(Vector query, int k, SearchAlgorithm searchMethod = SearchAlgorithm.KDTree, float similarityThreshold = 0.5f)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.k", k)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            try
            {
                var result = _searchService.Search(query: query, k, searchMethod, similarityThreshold: similarityThreshold);
                activity?.AddTag("search.result.count", result.Count);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).", query, k);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return new List<Vector>();
            }
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.Search(query: query, k, searchMethod, similarityThreshold: similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).", query, k);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return new List<Vector>();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    

    /// <summary>
    /// Searches for all vectors within a specified radius of the given text in the database.
    /// This text is first converted into an embedding using the EmbeddingGenerator.
    /// </summary>
    /// <param name="text">The text to search for</param>
    /// <param name="radius">The maximum distance from the query</param>
    /// <param name="searchMethod">Search algorithm to use</param>
    /// <param name="distanceCalculator">The distance calculator to use (defaults to Euclidean)</param>
    /// <returns>Vectors that are within the specified radius</returns>
    /// <seealso cref="EmbeddingGenerator"/>
    public IList<Vector> RangeSearch(string text, float radius, SearchAlgorithm searchMethod = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.radius", radius)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.RangeSearch(text, radius, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.RangeSearch(text, radius, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Searches for all vectors within a specified radius of the given query vector in the database.
    /// </summary>
    /// <param name="query">The query vector</param>
    /// <param name="radius">The maximum distance from the query</param>
    /// <param name="searchMethod">Search algorithm to use</param>
    /// <param name="distanceCalculator">The distance calculator to use (defaults to Euclidean)</param>
    /// <returns>Vectors that are within the specified radius</returns>
    public IList<Vector> RangeSearch(Vector query, float radius, SearchAlgorithm searchMethod = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.radius", radius)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.RangeSearch(query, radius, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.RangeSearch(query, radius, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Performs text search with metadata filtering support.
    /// </summary>
    /// <param name="text">The text to search for</param>
    /// <param name="k">Number of nearest neighbors to return</param>
    /// <param name="metadataFilter">Metadata filter to apply</param>
    /// <param name="searchMethod">The search algorithm to use</param>
    /// <param name="similarityThreshold">Similarity threshold for filtering results</param>
    /// <returns>A list of vectors matching the criteria, ordered by distance</returns>
    public IList<Vector> SearchWithMetadata(string text, int k, MetadataFilter? metadataFilter, SearchAlgorithm searchMethod = SearchAlgorithm.KDTree, float? similarityThreshold = null)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.k", k), new("search.hasMetadataFilter", metadataFilter?.HasFilters ?? false)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.SearchWithMetadata(text, k, metadataFilter, searchMethod, similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.SearchWithMetadata(text, k, metadataFilter, searchMethod, similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Performs vector search with metadata filtering support.
    /// </summary>
    /// <param name="query">The query vector</param>
    /// <param name="k">Number of nearest neighbors to return</param>
    /// <param name="metadataFilter">Metadata filter to apply</param>
    /// <param name="searchMethod">The search algorithm to use</param>
    /// <param name="similarityThreshold">Similarity threshold for filtering results</param>
    /// <returns>A list of vectors matching the criteria, ordered by distance</returns>
    public IList<Vector> SearchWithMetadata(Vector query, int k, MetadataFilter? metadataFilter, SearchAlgorithm searchMethod = SearchAlgorithm.KDTree, float similarityThreshold = 0.5f)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.k", k), new("search.hasMetadataFilter", metadataFilter?.HasFilters ?? false)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.SearchWithMetadata(query, k, metadataFilter, searchMethod, similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.SearchWithMetadata(query, k, metadataFilter, searchMethod, similarityThreshold);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Performs range search with metadata filtering support.
    /// </summary>
    /// <param name="text">The text to search for</param>
    /// <param name="radius">The maximum distance from the query</param>
    /// <param name="metadataFilter">Metadata filter to apply</param>
    /// <param name="searchMethod">The search algorithm to use</param>
    /// <param name="distanceCalculator">The distance calculator to use</param>
    /// <returns>A list of vectors within the specified radius and matching metadata criteria</returns>
    public IList<Vector> RangeSearchWithMetadata(string text, float radius, MetadataFilter? metadataFilter, SearchAlgorithm searchMethod = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.radius", radius), new("search.hasMetadataFilter", metadataFilter?.HasFilters ?? false)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.RangeSearchWithMetadata(text, radius, metadataFilter, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.RangeSearchWithMetadata(text, radius, metadataFilter, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Performs range search with metadata filtering support.
    /// </summary>
    /// <param name="query">The query vector</param>
    /// <param name="radius">The maximum distance from the query</param>
    /// <param name="metadataFilter">Metadata filter to apply</param>
    /// <param name="searchMethod">The search algorithm to use</param>
    /// <param name="distanceCalculator">The distance calculator to use</param>
    /// <returns>A list of vectors within the specified radius and matching metadata criteria</returns>
    public IList<Vector> RangeSearchWithMetadata(Vector query, float radius, MetadataFilter? metadataFilter, SearchAlgorithm searchMethod = SearchAlgorithm.Linear, IDistanceCalculator? distanceCalculator = null)
    {
        using var activity = StartActivity(tags: [new("search.searchMethod", searchMethod), new("search.radius", radius), new("search.hasMetadataFilter", metadataFilter?.HasFilters ?? false)]);

        // Lock-free path for immutable indexes
        if (CanUseLockFreeSearch(searchMethod))
        {
            var result = _searchService.RangeSearchWithMetadata(query, radius, metadataFilter, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }

        // Legacy path with read lock
        _rwLock.EnterReadLock();
        try
        {
            var result = _searchService.RangeSearchWithMetadata(query, radius, metadataFilter, searchMethod, distanceCalculator);
            activity?.AddTag("search.result.count", result.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    [LoggerMessage(
    EventId = 1,
    Level = LogLevel.Error,
    Message = "Could not perform range search for vector `{Query}` with radius {Radius} in the database.")]
    private partial void CouldNotPerformRangeSearchInDb(Vector query, float radius, Exception ex);

    [LoggerMessage(
    EventId = 0,
    Level = LogLevel.Error,
    Message = "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).")]
    private partial void CouldNotFindVectorInDb(Vector query, int k, Exception ex);

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorDatabase"/> class.
    /// </summary>
    public VectorDatabase()
        : this(Logging.LoggerFactory.CreateLogger<VectorDatabase>(), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorDatabase"/> class.
    /// </summary>
    /// <param name="logger">The logger to be used for logging.</param>
    /// <param name="instrumentation">The instrumentation to be used for metrics and tracing.</param>
    /// <exception cref="ArgumentNullException">Thrown when the logger is null.</exception>
    public VectorDatabase(ILogger<VectorDatabase> logger, Instrumentation? instrumentation)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _instrumentation = instrumentation ?? Instrumentation.Instance;

        _instrumentation.Meter.CreateObservableGauge(
            name: "neighborly.db.vectors.count",
            unit: "{vectors}",
            description: "The number of vectors in the database.",
            observeValue: () => Count,
            tags: [new("db.namespace", _id)]
        );
        _indexRebuildCounter = _instrumentation.Meter.CreateCounter<long>(
            name: "neighborly.db.index.rebuild",
            unit: "{rebuilds}",
            description: "The number of times the search index was rebuilt.",
            tags: [new("db.namespace", _id)]
        );
        _defaultTags = [new("db.system", "neighborly"), new("db.namespace", _id)];

        // Wire up the event handler for the VectorList.Modified event
        _vectors.Modified += VectorList_Modified;
        _searchService = new Search.SearchService(_vectors);
        StartIndexService();
    }

    #endregion

    #region Load/Save
    /// <summary>
    /// Loads vectors from a specified file path.
    /// </summary>
    /// <param name="path">The file path to load the vectors from.</param>
    /// <param name="createOnNew">Indicates whether to create a new file if it doesn't exist.</param>
    public async Task LoadAsync(string path, bool createOnNew = true, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(name: "LoadVectors");
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        var combinedToken = combinedCts.Token;
        string filePath = Path.Combine(path, "vectors.bin");
        bool fileExists = File.Exists(filePath);
        if (!createOnNew && !fileExists)
        {
            _logger.LogError("The file {FilePath} does not exist.", filePath);
            var error = $"The file {filePath} does not exist.";
            activity?.SetStatus(ActivityStatusCode.Error, error);
            throw new FileNotFoundException(error);
        }
        else if (createOnNew && !fileExists)
        {
            // Do nothing here. We'll create the file when SaveAsync() is called
            activity?.SetStatus(ActivityStatusCode.Ok, "File does not exist. Will create it when saving.");
            return;
        }
        else
        {
            bool indexesAreDirty;
            
            // Load data without holding lock for the entire operation
            _rwLock.EnterWriteLock();
            try
            {
                using (var inputStream = new FileStream(filePath, FileMode.Open))
                using (var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress))
                using (var reader = new BinaryReader(decompressionStream))
                {
                    _vectors.Clear();
                    (int vectorCount, indexesAreDirty) = await ReadFromAsync(reader, true, combinedToken).ConfigureAwait(false);
                    _logger.LogInformation("Loaded {VectorCount} vectors from {FilePath}.", vectorCount, inputStream.Name);
                }
            }
            finally
            {
                if (_rwLock.IsWriteLockHeld)
                {
                    _rwLock.ExitWriteLock();
                }
            }

            // Rebuild indexes outside the write lock to reduce lock contention
            if (indexesAreDirty && !_shutdownCts.IsCancellationRequested)
            {
                // Check for disposal/cancellation before rebuilding indexes
                combinedToken.ThrowIfCancellationRequested();
                await RebuildSearchIndexesAsync(combinedToken).ConfigureAwait(false);     // Rebuild both k-d tree and Ball Tree search index  
            }

            // Update state flags with minimal lock time
            _rwLock.EnterWriteLock();
            try
            {
                _vectors.Tags.BuildMap();   // Rebuild the tag map
                _hasUnsavedChanges = false; // Set the flag to indicate the database hasn't been modified
                _hasOutdatedIndex = false;  // Set the flag to indicate the index is up-to-date
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            finally
            {
                if (_rwLock.IsWriteLockHeld)
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }
    }

    internal async Task<(int vectorCount, bool indexesAreDirty)> ReadFromAsync(BinaryReader reader, bool includeIndexes, CancellationToken cancellationToken)
    {
        var fileVersion = reader.ReadInt32();   // File version

        Func<BinaryReader, bool, CancellationToken, Task<(int vectorCount, bool indexesAreDirty)>> importFunc = fileVersion switch
        {
            1 => LoadV1Async,
            _ => LoadV0Async
        };

        return await importFunc(reader, includeIndexes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports vectors from a specified file path with the V1 layout.
    /// </summary>
    /// <remarks>
    /// The V1 layout has a leading integer that indicates the total number of vectors in the database
    /// followed by the binary representation of each vector. Up to this, the V0 layout is the same.
    /// However, it is followed by the binary representation the indexes.
    /// </remarks>
    private async Task<(int vectorCount, bool indexesAreDirty)> LoadV1Async(BinaryReader reader, bool includeIndexes, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading vectors from the V1 layout.");
        var (vectorCount, _) = await LoadV0Async(reader, includeIndexes, cancellationToken).ConfigureAwait(false);

        if (includeIndexes)
        {
            await _searchService.LoadAsync(reader, cancellationToken).ConfigureAwait(false);
            return (vectorCount, false);
        }

        return (vectorCount, true);
    }

    /// <summary>
    /// Imports vectors from a specified file path with the original layout.
    /// </summary>
    /// <remarks>
    /// The original layout has a leading integer that indicates the total number of vectors in the database
    /// followed by the binary representation of each vector.
    /// </remarks>
    private Task<(int vectorCount, bool indexesAreDirty)> LoadV0Async(BinaryReader reader, bool includeIndexes, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading vectors from the original (V0) layout.");
        var vectorCount = reader.ReadInt32();   // Total number of Vectors in the database

        for (int i = 0; i < vectorCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nextVector = reader.ReadInt32();    // File offset of the next Vector
            var vector = new Vector(reader.ReadBytes(nextVector));
            _vectors.Add(vector);
        }

        return Task.FromResult((vectorCount, true));
    }

    /// <summary>
    /// Async background worker for periodic index rebuilding.
    /// Uses PeriodicTimer for cancellation-aware, async-native timed loops.
    /// </summary>
    private async Task IndexingWorkerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background indexing task started.");

        try
        {
            // Create PeriodicTimer with interval matching timeThresholdSeconds
            _indexingTimer = new PeriodicTimer(TimeSpan.FromSeconds(timeThresholdSeconds));

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for next tick - this is cancellation-aware
                    if (!await _indexingTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                    {
                        // Timer was disposed, exit loop
                        break;
                    }

                    // Check if rebuild is needed
                    if (_hasOutdatedIndex &&
                        _vectors.Count > 0 &&
                        DateTime.UtcNow.Subtract(_lastModification).TotalSeconds > timeThresholdSeconds)
                    {
                        await PerformIndexRebuildAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected during shutdown, exit gracefully
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background indexing task cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in background indexing task.");
        }
        finally
        {
            _logger.LogInformation("Background indexing task stopped.");
        }
    }

    /// <summary>
    /// Performs the actual index rebuild operations.
    /// Separated for clarity and testability.
    /// </summary>
    private async Task PerformIndexRebuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RebuildTagsAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            await RebuildSearchIndexesAsync(cancellationToken).ConfigureAwait(false);

            _indexRebuildCounter.Add(1);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Index rebuild cancelled during operation.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during background index rebuild.");
            // Don't rethrow - allow the worker to continue
        }
    }

    /// <summary>
    /// Starts the background indexing service.
    /// On mobile platforms (iOS/Android), background processing is disabled.
    /// </summary>
    private void StartIndexService()
    {
        // Background processing disabled on mobile platforms
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            _logger.LogInformation("Background indexing disabled on mobile platform.");
            return;
        }

        // Start the async background task on the thread pool
        _indexingTask = Task.Run(
            () => IndexingWorkerAsync(_shutdownCts.Token),
            _shutdownCts.Token);

        _logger.LogDebug("Background indexing service started.");
    }

    /// <summary>
    /// Stops the background indexing service gracefully.
    /// Guarantees completion within the specified timeout (default 5 seconds).
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown.</param>
    /// <returns>True if shutdown completed gracefully, false if timed out.</returns>
    private async Task<bool> StopIndexServiceAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);

        _logger.LogInformation("Stopping background indexing service...");

        // Dispose the timer first to unblock WaitForNextTickAsync
        _indexingTimer?.Dispose();
        _indexingTimer = null;

        if (_indexingTask is null || _indexingTask.IsCompleted)
        {
            _logger.LogDebug("Indexing task already completed or was never started.");
            return true;
        }

        try
        {
            // Signal cancellation using async cancel (.NET 8+)
            await _shutdownCts.CancelAsync().ConfigureAwait(false);

            // Wait for task with timeout using Task.WaitAsync (non-blocking)
            await _indexingTask.WaitAsync(timeout.Value).ConfigureAwait(false);

            _logger.LogInformation("Background indexing service stopped gracefully.");
            return true;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Background indexing task did not complete within {Timeout}s timeout. Proceeding with disposal anyway.",
                timeout.Value.TotalSeconds);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Indexing task cancellation acknowledged.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping indexing service.");
            return false;
        }
    }

    /// <summary>
    /// Synchronous wrapper for backwards compatibility with IDisposable.
    /// </summary>
    private void StopIndexService()
    {
        StopIndexServiceAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates a new kd-tree index for the vectors and a map of tags to vector IDs.
    /// (This searchMethod is eventually calls when the database is modified.)
    /// </summary>
    public Task RebuildTagsAsync(CancellationToken cancellationToken = default)
    {
        if (!_hasOutdatedIndex || _vectors == null || _vectors.Count == 0)
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Take snapshot for thread-safe iteration
        var snapshot = _vectors.ToList();
        if (snapshot.Count == 0)
        {
            return Task.CompletedTask;
        }

        using var activity = StartActivity(name: "RebuildTags");
        _vectors.Tags.BuildMap(snapshot);
        activity?.SetStatus(ActivityStatusCode.Ok);
        _hasOutdatedIndex = false;

        return Task.CompletedTask;
    }

    // This is an async function
    public async Task RebuildSearchIndexesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(name: "BuildAllSearchIndexes");
        cancellationToken.ThrowIfCancellationRequested();
        await _searchService.BuildAllIndexes(cancellationToken).ConfigureAwait(false);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
    public async Task RebuildSearchIndexAsync(SearchAlgorithm searchMethod = SearchAlgorithm.KDTree, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(name: "BuildSearchIndex");
        await _searchService.BuildIndexes(searchMethod, cancellationToken).ConfigureAwait(false);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Saves the vectors to the current directory.
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        // Get the current directory
        string currentDirectory = Directory.GetCurrentDirectory();

        // Call the existing Save searchMethod with the current directory
        await SaveAsync(currentDirectory, cancellationToken);
    }

    /// <summary>
    /// Saves the vectors to a specified file path atomically.
    /// Uses write-to-temp-then-rename pattern for crash-safe persistence.
    /// </summary>
    /// <param name="path">The directory path to save the vectors to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(name: "SaveVectors");

        string filePath = Path.Combine(path, "vectors.bin");
        // Temp file in same directory for atomic rename (cross-volume rename not atomic)
        string tempFilePath = Path.Combine(path, $"vectors.{Guid.NewGuid():N}.tmp");

        // Early exit if no changes - no lock needed for this volatile read
        if (!_hasUnsavedChanges)
        {
            _logger.LogInformation("The database has not been modified since the last save.");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return;
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogInformation("The directory {Path} was created.", path);
        }

        bool writeSucceeded = false;

        try
        {
            // READ lock - only reading vector data to serialize
            _rwLock.EnterReadLock();
            try
            {
                await using var outputStream = new FileStream(
                    tempFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true);

                await using (var compressionStream = new GZipStream(outputStream, CompressionLevel.Fastest, leaveOpen: true))
                await using (var writer = new BinaryWriter(compressionStream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    await WriteToAsync(writer, includeIndexes: true, cancellationToken).ConfigureAwait(false);
                }

                // Flush to disk before rename to ensure durability
                outputStream.Flush(flushToDisk: true);
            }
            finally
            {
                if (_rwLock.IsReadLockHeld)
                {
                    _rwLock.ExitReadLock();
                }
            }

            // Atomic rename - this is the commit point
            File.Move(tempFilePath, filePath, overwrite: true);
            writeSucceeded = true;

            _logger.LogInformation("Saved the database atomically to {FilePath}.", filePath);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Save operation was cancelled.");
            activity?.SetStatus(ActivityStatusCode.Error, "Operation cancelled");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while saving the database to {FilePath}.", filePath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error while saving the database to {FilePath}.", filePath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            // Always clean up temp file
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp file {TempFilePath}.", tempFilePath);
                }
            }

            if (writeSucceeded)
            {
                _hasUnsavedChanges = false;
            }
        }
    }

    internal async Task WriteToAsync(BinaryWriter writer, bool includeIndexes, CancellationToken cancellationToken = default)
    {
        // TODO -- This should be async and potentially parallelized
        writer.Write(s_currentFileVersion);
        writer.Write(_vectors.Count);
        foreach (Vector v in _vectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes = v.ToBinary();
            writer.Write(bytes.Length);    // File offset of the next Vector
            writer.Write(bytes);           // The Vector itself
        }

        if (includeIndexes)
        {
            await _searchService.SaveAsync(writer, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region Import/Export

    [LoggerMessage(
        EventId = 9_000,
        Level = LogLevel.Debug,
        Message = "Importing vectors from {ContentType} source {Path}.")]
    private partial void ImportingData(ContentType contentType, string path);

    [LoggerMessage(
        EventId = 9_001,
        Level = LogLevel.Information,
        Message = "Vectors were imported source {Path}.")]
    private partial void ImportedData(string path);

    public async Task ImportDataAsync(string path, bool isDirectory, ContentType contentType, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(tags: [new("import.contentType", contentType), new("import.isDirectory", isDirectory)]);
        _rwLock.EnterWriteLock();
        try
        {
            ImportingData(contentType, path);
            IETL etl = EtlFactory.CreateEtl(contentType);
            etl.IsDirectory = isDirectory;
            await etl.ImportDataAsync(path, Vectors, cancellationToken).ConfigureAwait(false);
            ImportedData(path);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        finally
        {
            if (_rwLock.IsWriteLockHeld)
            {
                _rwLock.ExitWriteLock();
            }
        }
    }

    [LoggerMessage(
        EventId = 9_010,
        Level = LogLevel.Debug,
        Message = "Exporting {VectorCount} vectors to {Path} with type {ContentType}.")]
    private partial void ExportingData(int vectorCount, string path, ContentType contentType);

    [LoggerMessage(
        EventId = 9_011,
        Level = LogLevel.Information,
        Message = "Vectors were exported to {Path}.")]
    private partial void ExportedData(string path);

    public async Task ExportDataAsync(string path, ContentType contentType, CancellationToken cancellationToken = default)
    {
        using var activity = StartActivity(tags: [new("export.contentType", contentType)]);
        _rwLock.EnterReadLock();
        try
        {
            ExportingData(Vectors.Count, path, contentType);
            IETL etl = EtlFactory.CreateEtl(contentType);
            await etl.ExportDataAsync(Vectors, path, cancellationToken).ConfigureAwait(false);
            ExportedData(path);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        finally
        {
            if (_rwLock.IsReadLockHeld)
            {
                _rwLock.ExitReadLock();
            }
        }
    }
    #endregion

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Check if async disposal already happened (thread-safe single-entry)
                if (Interlocked.CompareExchange(ref _asyncDisposeStarted, 1, 0) == 0)
                {
                    // Async dispose hasn't run - do sync cleanup
                    _logger.LogInformation("Disposing VectorDatabase synchronously...");

                    // Stop indexing service (uses sync wrapper with 5s timeout)
                    StopIndexService();

                    // Dispose timer if not already done
                    _indexingTimer?.Dispose();

                    // Cleanup vectors with write lock
                    _rwLock.EnterWriteLock();
                    try
                    {
                        _vectors.Modified -= VectorList_Modified;
                        _vectors.Dispose();
                    }
                    finally
                    {
                        _rwLock.ExitWriteLock();
                    }

                    // Dispose synchronization primitives
                    _rwLock.Dispose();
                    _shutdownCts.Dispose();

                    _logger.LogInformation("VectorDatabase disposed.");
                }
                // If _asyncDisposeStarted was already 1, async dispose handled cleanup
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources asynchronously, allowing graceful shutdown of background tasks.
    /// Implements the async disposal pattern from Microsoft guidelines.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);

        // Suppress finalization since we've disposed
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Core async disposal logic - handles async cleanup operations.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        // Ensure single-entry with thread-safety
        if (Interlocked.CompareExchange(ref _asyncDisposeStarted, 1, 0) != 0)
        {
            return; // Already disposing
        }

        if (_disposedValue)
        {
            return;
        }

        _logger.LogInformation("Disposing VectorDatabase asynchronously...");

        try
        {
            // Step 1: Stop background indexing gracefully (5 second timeout)
            await StopIndexServiceAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            // Step 2: Dispose the PeriodicTimer if not already done
            _indexingTimer?.Dispose();

            // Step 3: Cleanup vectors and event handlers
            _rwLock.EnterWriteLock();
            try
            {
                _vectors.Modified -= VectorList_Modified;
                _vectors.Dispose();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            // Step 4: Dispose synchronization primitives
            _rwLock.Dispose();
            _shutdownCts.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during async disposal.");
        }
        finally
        {
            _disposedValue = true;
        }

        _logger.LogInformation("VectorDatabase disposed.");
    }

    /// <summary>
    /// StartActivity is responsible for starting a new activity for tracing and telemetry purposes. 
    /// This function leverages the ActivitySource class from the System.Diagnostics namespace to create and start an activity, 
    /// which can be used to track the execution of code and collect telemetry data.
    /// </summary>
    /// <param name="kind">Specifies the kind of activity (e.g., internal, server, client). The default is ActivityKind.Internal.</param>
    /// <param name="parentContext">Provides the context of the parent activity, if any. The default is an empty context.</param>
    /// <param name="tags">A collection of key-value pairs representing tags to be associated with the activity. The default is null.</param>
    /// <param name="links">A collection of links to other activities. The default is null.</param>
    /// <param name="startTime">The start time of the activity. The default is the current time.</param>
    /// <param name="name"> The name of the activity. The default is the name of the calling method, provided by the CallerMemberName attribute.</param>
    /// <returns></returns>
    private Activity? StartActivity(ActivityKind kind = ActivityKind.Internal, ActivityContext parentContext = default, IEnumerable<KeyValuePair<string, object?>>? tags = null, IEnumerable<ActivityLink>? links = null, DateTimeOffset startTime = default, [CallerMemberName] string name = "")
    {
        if (tags is null)
        {
            tags = _defaultTags;
        }
        else
        {
            tags = [.. tags, .. _defaultTags];
        }

        return _instrumentation.ActivitySource.StartActivity(kind, parentContext, tags, links, startTime, name);
    }
}
