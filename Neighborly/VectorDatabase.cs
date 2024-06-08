using Neighborly;
using Neighborly.ETL;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Reflection.PortableExecutable;

namespace Neighborly;

/// <summary>
/// Represents a database for storing and searching vectors.
/// </summary>
public partial class VectorDatabase // : ICollection<Vector>
{
    private readonly ILogger<VectorDatabase> _logger;
    private VectorList _vectors = new();
    public VectorList Vectors => _vectors;
    private KDTree _kdTree = new();
    private ISearchMethod _searchStrategy = new LinearSearch();
    private ReaderWriterLockSlim _rwLock = new();
    private StorageOptionEnum _storageOption = StorageOptionEnum.Auto;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorDatabase"/> class.
    /// </summary>
    public VectorDatabase()
        : this(NullLogger<VectorDatabase>.Instance)
    {
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

        // Wire up the event handler for the VectorList.Modified event
        _vectors.Modified += VectorList_Modified;
        StartIndexService();
    }

    /// <summary>
    /// Gets the number of vectors in the database.
    /// </summary>
    public int Count => _vectors.Count;

    /// <summary>
    /// Gets a value indicating whether the database is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    private bool _hasUnsavedChanges = false;
    private bool _hasOutdatedIndex = false;

    /// <summary>
    /// Gets a value indicating whether the database has been modified since the last save.
    /// </summary>
    public bool IsDirty { get { return _hasUnsavedChanges; } }

    private void VectorList_Modified(object? sender, EventArgs e)
    {
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

    /// <summary>
    /// Gets or sets the search method used by the database.
    /// This can be changed at runtime to switch between search methods.
    /// </summary>
    public ISearchMethod SearchMethod
    {
        get { return _searchStrategy; }
        set { _searchStrategy = value; }
    }

    /// <summary>
    /// Searches for the k nearest neighbors to a given query vector.
    /// </summary>
    /// <param name="query">The query vector.</param>
    /// <param name="k">The number of nearest neighbors to retrieve.</param>
    /// <returns>A list of the k nearest neighbors to the query vector.</returns>
    /// TODO -- Search should move out of VectorDatabase 
    public IList<Vector> Search(Vector query, int k)
    {
        try
        {
            return _searchStrategy.Search(_vectors, query, k);
        }
        catch (Exception ex)
        {
            // Log the exception, if you have a logging system
            CouldNotFindVectorInDb(query, k, ex);

            // return an empty list if an exception occurs
            return new List<Vector>();
        }
    }
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
            throw new FileNotFoundException($"The file {filePath} does not exist.");
        }
        else if (createOnNew && !fileExists) 
        {
            // We'll create the file when SaveAsync() is called
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
                    while (reader.BaseStream.Position != reader.BaseStream.Length)
                    {
                        var vectorCount = reader.ReadInt32();   // Total number of Vectors in the database

                        for (int i = 0; i < vectorCount; i++)
                        {
                            var nextVector = reader.ReadInt32();    // File offset of the next Vector
                            var vector = new Vector(reader.ReadBytes(nextVector));
                            _vectors.Add(vector);
                        }
                    }
                }

                _kdTree.Build(_vectors); // Rebuild the KDTree with the new vectors
                _vectors.Tags.BuildMap(); // Rebuild the tag map
                _hasUnsavedChanges = false; // Set the flag to indicate the database hasn't been modified
                _hasOutdatedIndex = false; // Set the flag to indicate the index is up-to-date
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

    }

    private void StartIndexService() 
    {
        // Create a new thread that will react when _hasOutdatedIndex is set to true
        var indexService = new Thread(() => 
        {
            while (!_vectors.IsReadOnly)
            {
                if (_hasOutdatedIndex && _vectors.Count > 0)
                {
                    RebuildIndex();
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
    public void RebuildIndex()
    {
        if (!_hasOutdatedIndex || _vectors == null || _vectors.Count == 0)
        {
            return;
        }
        lock (_kdTree)
        {
            _kdTree.Build(_vectors);
        }
        _vectors.Tags.BuildMap();
        _hasOutdatedIndex = false;
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
    /// (In the gRPC server, this method will be called when the host OS sends a shutdown signal.)
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
        _rwLock.ExitWriteLock();
        _hasUnsavedChanges = false; // Set the flag to indicate the database hasn't been modified
    }
    #endregion

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).")]
    public partial void CouldNotFindVectorInDb(Vector query, int k, Exception ex);

    #region Import/Export
    public async Task ImportDataAsync(string path, bool isDirectory, ETL.ContentType contentType)
    {
        ETL.IETL etl;

        switch (contentType)
        {
            case ETL.ContentType.Parquet:
                etl = new ETL.Parquet();
                break;
            case ETL.ContentType.CSV:
                etl = new ETL.Csv();
                break;
            case ETL.ContentType.HDF5:
                etl = new ETL.HDF5();
                break;
            default:
                throw new NotSupportedException($"Content type {contentType} is not supported.");
        }

        etl.isDirectory = isDirectory;
        etl.vectorDatabase = this;
        await etl.ImportDataAsync(path);
    }

    Task ExportDataAsync(string path, ETL.ContentType contentType)
    {
        throw new NotImplementedException();
    }
    #endregion

}
