using Neighborly;
using System.Collections;
using System.Collections.Generic;

namespace Neighborly;
public class VectorDatabase : ICollection<Vector>
{
    private DiskBackedList<Vector> _vectors = new DiskBackedList<Vector>();
    private KDTree _kdTree = new KDTree();
    private ISearchMethod _searchStrategy = new LinearSearch();
    private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
    private StorageOptionEnum _storageOption = StorageOptionEnum.Auto;
    public int Count => _vectors.Count;

    public bool IsReadOnly => false;

    public StorageOptionEnum StorageOption
    {
        // TODO: Implement the ability to move the storage from memory to disk while in operation
        get { return _storageOption; } 
    }
    public ISearchMethod SearchMethod
    {
        get { return _searchStrategy; }
        set { _searchStrategy = value; }
    }

    public void AddRange(IEnumerable<Vector> items)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _vectors.AddRange(items);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public void RemoveRange(IEnumerable<Vector> items)
    {
        _rwLock.EnterWriteLock();
        try
        {
            foreach (var item in items)
            {
                _vectors.Remove(item);
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

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
                return true;
            }
        }
        finally { _rwLock.ExitWriteLock(); }
        return false;
    }

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
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _vectors.Clear();
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public bool Contains(Vector item)
    {
        return _vectors.Contains(item);
    }

    public void CopyTo(Vector[] array, int arrayIndex)
    {
        _vectors.CopyTo(array, arrayIndex);
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        return _vectors.GetEnumerator();
    }

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
            }
        }
        finally { _rwLock.ExitWriteLock(); }
        return result;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IList<Vector> Search(Vector query, int k)
    {
        try
        {
            return _searchStrategy.Search(_vectors, query, k);
        }
        catch (Exception ex)
        {
            // Log the exception, if you have a logging system
            // Console.WriteLine(ex);

            // Handle the exception
            // This could involve cleaning up resources, showing an error message to the user, etc.

            // Rethrow the exception if you want it to be handled at a higher level
            // throw;

            // Or throw a new exception
            // throw new ApplicationException("An error occurred while searching for vectors.", ex);

            // If you don't want to throw an exception, you can return a default value
            return new List<Vector>();
        }
    }

    public void Load(string path)
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
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file {filePath} does not exist.");
        }

        _rwLock.EnterWriteLock();
        try
        {
            var bytes = HelperFunctions.Decompress(HelperFunctions.ReadFromFile(filePath));
            _vectors = HelperFunctions.DeserializeFromBinary<DiskBackedList<Vector>>(bytes);

            _kdTree.Build(_vectors); // Rebuild the KDTree with the new vectors
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
#endif

    }

    public List<Vector> FindAll(Predicate<Vector> match)
    {
        return _vectors.FindAll(match);
    }

    public Vector Find(Predicate<Vector> match)
    {
        return _vectors.Find(match);
    }

    public void Save(string path)
    {
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

    }

}
