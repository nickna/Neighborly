using Neighborly.ETL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using Neighborly.Search;
using CsvHelper;

namespace Neighborly;

/// <summary>
/// Represents a database for storing and searching vectors.
/// </summary>
public partial class VectorDatabase
{
    private readonly ILogger<VectorDatabase> _logger = Logging.LoggerFactory.CreateLogger<VectorDatabase>();
    private readonly VectorList _vectors = new();
    public VectorList Vectors => _vectors;
    private Search.SearchService _searchService;
    private ReaderWriterLockSlim _rwLock = new();
    private StorageOptionEnum _storageOption = StorageOptionEnum.Auto;

    /// <summary>
    /// Last time the database was modified. This is updated when a vector is added or removed.
    /// </summary>
    private DateTime lastModification = DateTime.Now;

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

    /// <summary>
    /// Gets a value indicating whether the database has been modified since the last save.
    /// </summary>
    public bool HasUnsavedChanges { get { return _hasUnsavedChanges; } }

    private void VectorList_Modified(object? sender, EventArgs e)
    {
        lastModification = DateTime.Now;
        _hasUnsavedChanges = true;
        _hasOutdatedIndex = true;
    }

    /// <summary>
    /// Gets the storage option used by the database.
    /// </summary>
    public StorageOptionEnum StorageOption
    {
        // TODO: Implement the ability to move the storage from memory to disk while in operation
        get { return _storageOption; }
    }

    public IList<Vector> Search(Vector query, int k, SearchAlgorithm searchMethod = SearchAlgorithm.KDTree)
    {
        try
        {
            return _searchService.Search(query, k);
        }
        catch (Exception ex)
        {
            CouldNotFindVectorInDb(query, k, ex);
            return new List<Vector>();
        }
            
    }

    [LoggerMessage(
    EventId = 0,
    Level = LogLevel.Error,
    Message = "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).")]
    public partial void CouldNotFindVectorInDb(Vector query, int k, Exception ex);

    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorDatabase"/> class.
    /// </summary>
    public VectorDatabase()
        : this(Logging.LoggerFactory.CreateLogger<VectorDatabase>())
    {
        // Wire up the event handler for the VectorList.Modified event
        _vectors.Modified += VectorList_Modified;
        _searchService = new Search.SearchService(_vectors);
        StartIndexService();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorDatabase"/> class.
    /// </summary>
    /// <param name="logger">The logger to be used for logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when the logger is null.</exception>
    public VectorDatabase(ILogger<VectorDatabase> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
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
    public async Task LoadAsync(string path, bool createOnNew = true)
    {
        string filePath = Path.Combine(path, "vectors.bin");
        bool fileExists = File.Exists(filePath);
        if (!createOnNew && !fileExists)
        {
            _logger.LogError($"The file {filePath} does not exist.");
            throw new FileNotFoundException($"The file {filePath} does not exist.");
        }
        else if (createOnNew && !fileExists)
        {
            // Do nothing here. We'll create the file when SaveAsync() is called
            return;
        }
        else
        {
            _rwLock.EnterWriteLock();
            try
            {
                using (var inputStream = new FileStream(filePath, FileMode.Open))
                using (var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress))
                using (var reader = new BinaryReader(decompressionStream))
                {
                    _vectors.Clear();
                    var vectorCount = reader.ReadInt32();   // Total number of Vectors in the database

                    for (int i = 0; i < vectorCount; i++)
                    {
                        var nextVector = reader.ReadInt32();    // File offset of the next Vector
                        var vector = new Vector(reader.ReadBytes(nextVector));
                        _vectors.Add(vector);
                    }
                    Console.WriteLine(inputStream.Name);
                    reader.Close();
                    inputStream.Close();
                }

                await RebuildSearchIndexesAsync();     // Rebuild both k-d tree and Ball Tree search index  
                
                _vectors.Tags.BuildMap();   // Rebuild the tag map
                _hasUnsavedChanges = false; // Set the flag to indicate the database hasn't been modified
                _hasOutdatedIndex = false;  // Set the flag to indicate the index is up-to-date
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

    private void StartIndexService()
    {
        // The index service is not supported on mobile platforms
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            return;

        // Create a new thread that will react when _hasOutdatedIndex is set to true
        var indexService = new Thread(async () => 
        {
            while (!_vectors.IsReadOnly)
            {
                // If the database has been modified and the last modification was more than 5 seconds ago, rebuild the indexes
                if (_hasOutdatedIndex && 
                    _vectors.Count > 0 && 
                    DateTime.Now.Subtract(lastModification).TotalSeconds > timeThresholdSeconds)
                {
                    await RebuildTagsAsync();
                    await RebuildSearchIndexesAsync();
                }
                Thread.Sleep(5000);
            }
        });
        indexService.Priority = ThreadPriority.Lowest;
    }

    /// <summary>
    /// Creates a new kd-tree index for the vectors and a map of tags to vector IDs.
    /// (This method is eventually calls when the database is modified.)
    /// </summary>
    public async Task RebuildTagsAsync()
    {
        if (!_hasOutdatedIndex || _vectors == null || _vectors.Count == 0)
        {
            return;
        }
        await Task.Run (() => _vectors.Tags.BuildMap());
        _hasOutdatedIndex = false;
    }

    // This is an async function
    public async Task RebuildSearchIndexesAsync()
    {
        await Task.Run(() => _searchService.BuildAllIndexes());
    }
    public async Task RebuildSearchIndexAsync(SearchAlgorithm searchMethod = SearchAlgorithm.KDTree)
    {
        await Task.Run(() => _searchService.BuildIndex(searchMethod));
    }

    /// <summary>
    /// Saves the vectors to the current directory.
    /// </summary>
    public async Task SaveAsync()
    {
        // Get the current directory
        string currentDirectory = Directory.GetCurrentDirectory();

        // Call the existing Save method with the current directory
        await SaveAsync(currentDirectory);
    }

    /// <summary>
    /// Saves the vectors to a specified file path.
    /// (In the API server, this method will be called when the host OS sends a shutdown signal.)
    /// </summary>
    /// <param name="path">The file path to save the vectors to.</param>
    public async Task SaveAsync(string path)
    {
        // If the database hasn't been modified, no need to save it
        if (!_hasUnsavedChanges)
        {
            return;
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        try
        {
            _rwLock.EnterWriteLock();
            // Save the vectors to a binary file
            string filePath = Path.Combine(path, "vectors.bin");
            var outputStream = new FileStream(filePath, FileMode.Create);
            // TODO -- Experiment with other compression types. For now, GZip works.
            using (var compressionStream = new GZipStream(outputStream, CompressionLevel.Fastest))
            using (var writer = new BinaryWriter(compressionStream))
            {
                // TODO -- This should be async and potentially parallelized
                writer.Write(_vectors.Count);
                foreach (Vector v in _vectors)
                {
                    byte[] bytes = v.ToBinary();
                    writer.Write(bytes.Length);    // File offset of the next Vector
                    writer.Write(bytes);           // The Vector itself
                }
                writer.Close();
                outputStream.Close();
            }

            _hasUnsavedChanges = false; // Set the flag to indicate the database hasn't been modified
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "An error occurred while saving the database. Access to the path is denied.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "An error occurred while saving the database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while saving the database.");
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        
    }
    #endregion

    #region Import/Export
    public Task ImportDataAsync(string path, bool isDirectory, ContentType contentType, CancellationToken cancellationToken = default)
    {
        IETL etl = EtlFactory.CreateEtl(contentType);
        etl.IsDirectory = isDirectory;
        return etl.ImportDataAsync(path, Vectors, cancellationToken);
    }

    public Task ExportDataAsync(string path, ContentType contentType, CancellationToken cancellationToken = default)
    {
        IETL etl = EtlFactory.CreateEtl(contentType);
        return etl.ExportDataAsync(Vectors, path, cancellationToken);
    }
    #endregion

}
