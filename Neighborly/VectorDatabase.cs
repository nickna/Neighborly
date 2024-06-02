using Neighborly;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    public VectorDatabase()
        : this(NullLogger<VectorDatabase>.Instance)
    {
    }

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
        _rwLock.EnterWriteLock();
        try
        {
            foreach (var item in items)
            {
                _vectors.Remove(item);
            }
            _isDirty = true; // Set the flag to indicate the database has been modified
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Updates an existing vector in the database.
    /// Searches for the vector by its ID.
    /// </summary>
    /// <param name="vector"></param>
    /// <returns>True if the Vector was updated; otherwise, false.</returns>
    public bool Update(Vector vector)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var index = _vectors.FindIndex(v => v.Id == vector.Id);
            if (index != -1)
            {
                _vectors[index] = vector;
                _isDirty = true; // Set the flag to indicate the database has been modified
                return true;
            }
        }
        finally { _rwLock.ExitWriteLock(); }
        return false;
    }


    /// <summary>
    /// Updates an existing vector in the database.
    /// </summary>
    /// <param name="oldItem">The vector to be updated.</param>
    /// <param name="newItem">The updated vector.</param>
    /// <returns>True if the vector was successfully updated; otherwise, false.</returns>
    public bool Update(Vector oldItem, Vector newItem)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (oldItem == null)
            {
                throw new ArgumentNullException(nameof(oldItem), "Vector cannot be null");
            }
            if (newItem == null)
            {
                throw new ArgumentNullException(nameof(newItem), "Vector cannot be null");
            }

            var index = _vectors.IndexOf(oldItem);
            if (index != -1)
            {
                _vectors[index] = newItem;
                _isDirty = true; // Set the flag to indicate the database has been modified
                return true;
            }
        }
        finally { _rwLock.ExitWriteLock(); }
        return false;
    }

    /// <summary>
    /// Adds a vector to the database.
    /// </summary>
    /// <param name="item">The vector to add.</param>
    public void Add(Vector item)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "Vector cannot be null");
            }

            _vectors.Add(item);
            _kdTree.Build(_vectors);
            _isDirty = true; // Set the flag to indicate the database has been modified
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    /// <summary>
    /// Removes all vectors from the database.
    /// </summary>
    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _vectors.Clear();
            _isDirty = true; // Set the flag to indicate the database has been modified
        }
        finally { _rwLock.ExitWriteLock(); }
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
        _rwLock.EnterWriteLock();
        bool result = false;
        try
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "Vector cannot be null");
            }

            result = _vectors.Remove(item);
            if (result)
            {
                _kdTree.Build(_vectors);
                _isDirty = true; // Set the flag to indicate the database has been modified
            }
        }
        finally { _rwLock.ExitWriteLock(); }
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
    public void Load(string path, bool createOnNew = true)
    {
#if Save_VectorFiles
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The directory {path} does not exist.");
        }
        _rwLock.EnterWriteLock();
        try
        {
            _vectors.Clear(); // Clear the current vectors

            var files = Directory.GetFiles(path, "vector_*.txt");
            foreach (var file in files)
            {
                var vectorData = File.ReadAllBytes(file);
                var vector = Vector.Parse(vectorData); // Assuming Vector has a Parse method
                _vectors.Add(vector);
            }

            _kdTree.Build(_vectors); // Rebuild the KDTree with the new vectors
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
#else
        string filePath = Path.Combine(path, "vectors.bin");
        bool fileExists = File.Exists(filePath);
        if (!createOnNew && !fileExists)
        {
            throw new FileNotFoundException($"The file {filePath} does not exist.");
        }
        else if (createOnNew && !fileExists)
        {
            return;
        }
        else
        {
            _rwLock.EnterWriteLock();
            try
            {
                var bytes = HelperFunctions.Decompress(HelperFunctions.ReadFromFile(filePath));
                _vectors = HelperFunctions.DeserializeFromBinary<DiskBackedList<Vector>>(bytes);

                _kdTree.Build(_vectors); // Rebuild the KDTree with the new vectors
                _isDirty = false; // Set the flag to indicate the database hasn't been modified
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
#endif

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
    public void Save()
    {
        // Get the current directory
        string currentDirectory = Directory.GetCurrentDirectory();

        // Call the existing Save method with the current directory
        Save(currentDirectory);
    }

    /// <summary>
    /// Saves the vectors to a specified file path.
    /// (In the gRPC server, this method will be called when the host OS sends a shutdown signal.)
    /// </summary>
    /// <param name="path">The file path to save the vectors to.</param>
    public void Save(string path)
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

#if Save_VectorFiles
        // Save the vectors to disk
        // Assuming each vector is saved as a separate file
        for (int i = 0; i < _vectors.Count; i++)
        {
            string fileName = $"vector_{i}.txt";
            string filePath = Path.Combine(path, fileName);
            File.WriteAllBytes(filePath, _vectors[i].ToBinary());
        }
#else
        // Save the vectors to a binary file
        string filePath = Path.Combine(path, "vectors.bin");
        var bytes = HelperFunctions.SerializeToBinary(_vectors);
        HelperFunctions.WriteToFile(filePath, HelperFunctions.Compress(bytes));
#endif
        _isDirty = false; // Set the flag to indicate the database hasn't been modified
    }

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Could not find vector `{Query}` in the database searching the {k} nearest neighbor(s).")]
    public partial void CouldNotFindVectorInDb(Vector query, int k, Exception ex);

}
