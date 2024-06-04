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
public partial class VectorDatabase : ICollection<Vector>
{
    private readonly ILogger<VectorDatabase> _logger;
    private DiskBackedList<Vector> _vectors = new DiskBackedList<Vector>();
    private KDTree _kdTree = new KDTree();
    private ISearchMethod _searchStrategy = new LinearSearch();
    private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
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
    }

    /// <summary>
    /// Gets the number of vectors in the database.
    /// </summary>
    public int Count => _vectors.Count;

    /// <summary>
    /// Gets a value indicating whether the database is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    private bool _isDirty = false;

    /// <summary>
    /// Gets a value indicating whether the database has been modified since the last save.
    /// </summary>
    public bool IsDirty => _isDirty;

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
    /// Adds a range of vectors to the database.
    /// </summary>
    /// <param name="items">The collection of vectors to add.</param>
    public void AddRange(IEnumerable<Vector> items)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _vectors.AddRange(items);
            _isDirty = true; // Set the flag to indicate the database has been modified
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    /// <summary>
    /// Removes a range of vectors from the database.
    /// </summary>
    /// <param name="items">The collection of vectors to remove.</param>
    public void RemoveRange(IEnumerable<Vector> items)
    {
        _vectors.RemoveRange(items);
        _isDirty = true; // Set the flag to indicate the database has been modified
    }

    /// <summary>
    /// Updates an existing vector in the database.
    /// Searches for the vector by its ID.
    /// </summary>
    /// <param name="vector"></param>
    /// <returns>True if the Vector was updated; otherwise, false.</returns>
    public bool Update(Vector vector)
    {
        _vectors.Update(vector);
        return true;
    }


    /// <summary>
    /// Updates an existing vector in the database.
    /// </summary>
    /// <param name="oldItem">The vector to be updated.</param>
    /// <param name="newItem">The updated vector.</param>
    /// <returns>True if the vector was successfully updated; otherwise, false.</returns>
    public bool Update(Vector oldItem, Vector newItem)
    {
        _vectors.Update(oldItem, newItem);
        _isDirty = true; // Set the flag to indicate the database has been modified
        return false;
    }

    /// <summary>
    /// Adds a vector to the database.
    /// </summary>
    /// <param name="item">The vector to add.</param>
    public void Add(Vector item)
    {
        _vectors.Add(item);
        _kdTree.Build(_vectors);
        _isDirty = true; // Set the flag to indicate the database has been modified
    }

    /// <summary>
    /// Removes all vectors from the database.
    /// </summary>
    public void Clear()
    {        
        _vectors.Clear();
        _isDirty = true; // Set the flag to indicate the database has been modified   
    }

    /// <summary>
    /// Determines whether the database contains a specific vector.
    /// </summary>
    /// <param name="item">The vector to locate in the database.</param>
    /// <returns>True if the vector is found in the database; otherwise, false.</returns>
    public bool Contains(Vector item)
    {
        return _vectors.Contains(item);
    }

    /// <summary>
    /// Copies the vectors of the database to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the database.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(Vector[] array, int arrayIndex)
    {
        _vectors.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the vectors in the database.
    /// </summary>
    /// <returns>An enumerator for the vectors in the database.</returns>
    public IEnumerator<Vector> GetEnumerator()
    {
        return _vectors.GetEnumerator();
    }

    /// <summary>
    /// Removes a specific vector from the database.
    /// </summary>
    /// <param name="item">The vector to remove.</param>
    /// <returns>True if the vector was successfully removed; otherwise, false.</returns>
    public bool Remove(Vector item)
    {
        var result = _vectors.Remove(item);
        if (result)
        {
            _kdTree.Build(_vectors);
            _isDirty = true; // Set the flag to indicate the database has been modified
        }
        return result;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the vectors in the database.
    /// </summary>
    /// <returns>An enumerator for the vectors in the database.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Searches for the k nearest neighbors to a given query vector.
    /// </summary>
    /// <param name="query">The query vector.</param>
    /// <param name="k">The number of nearest neighbors to retrieve.</param>
    /// <returns>A list of the k nearest neighbors to the query vector.</returns>
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
                _isDirty = false; // Set the flag to indicate the database hasn't been modified
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

    }

    /// <summary>
    /// Retrieves all vectors that match a specified predicate.
    /// </summary>
    /// <param name="match">The predicate to match against.</param>
    /// <returns>A list of vectors that match the specified predicate.</returns>
    public List<Vector> FindAll(Predicate<Vector> match)
    {
        return _vectors.FindAll(match);
    }

    /// <summary>
    /// Retrieves the first vector that matches a specified predicate.
    /// </summary>
    /// <param name="match">The predicate to match against.</param>
    /// <returns>The first vector that matches the specified predicate.</returns
    public Vector Find(Predicate<Vector> match)
    {
        return _vectors.Find(match);
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
        if (!_isDirty)
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
        _isDirty = false; // Set the flag to indicate the database hasn't been modified
    }


    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).")]
    public partial void CouldNotFindVectorInDb(Vector query, int k, Exception ex);

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

}
