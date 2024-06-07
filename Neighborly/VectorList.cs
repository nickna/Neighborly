using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Neighborly;
namespace Neighborly;

/// <summary>
/// List that stores items in memory up to a certain count, then spills over to disk.
/// </summary>
public class VectorList : IList<Neighborly.Vector>, IDisposable
{
    private List<Neighborly.Vector?> _vectorList = new();
    private List<Guid> _guids = new();
    public List<Guid> Guids
    {
        get { return _guids; }
    }
    private List<string> _onDiskFilePaths = new();
    private VectorTags _tags = new();
    public VectorTags Tags => _tags;
    private int _maxInMemoryCount;
    private readonly object _lock = new();
    private bool _disposed = false;

    /// <summary>
    /// Event that is triggered when data has changed
    /// </summary>
    public event EventHandler Modified;

    /// <summary>
    /// Creates a new instance of DiskBackedList with a maximum in-memory count based on system memory.
    /// </summary>
    public VectorList()
    {
        this._tags.VectorList = this;
    }

    /// <summary>
    /// This releases the file resources allocated to each Vector object when the list is disposed.
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.
                _vectorList.Clear();
            }

            // Dispose unmanaged resources.
            foreach (var filePath in _onDiskFilePaths)
            {
                if (filePath != string.Empty && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            _disposed = true;
        }
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~VectorList()
    {
        Dispose(false);
    }

    /// <summary>
    /// Creates a new instance and Sets the maximum in-memory count.
    /// </summary>
    /// <param name="maxInMemoryCount"></param>
    public VectorList(int maxInMemoryCount)
    {
        _maxInMemoryCount = maxInMemoryCount;
    }

    public void Add(Neighborly.Vector item)
    {
        lock (_lock)
        {
            _onDiskFilePaths.Add(string.Empty);
            _vectorList.Add(item);
            _guids.Add(item.Id);

            var itemIndex = _vectorList.Count - 1;

            if (_vectorList.Count >= _maxInMemoryCount)
            {
                MoveToDisk(itemIndex);
            }
        }
        Modified?.Invoke(this, EventArgs.Empty);

    }

    public void AddRange(IEnumerable<Neighborly.Vector> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public Neighborly.Vector Get(int index)
    {
        lock (_lock)
        {
            if (_vectorList[index] != null)
            {
                // Vector found in memory
                return _vectorList[index]!;
            }
            else if (_onDiskFilePaths[index] != string.Empty)
            {
                // Vector exists on disk
                return ReadFromDisk(_onDiskFilePaths[index]);
            }
            else if (index > this.Count || index < 0)
            {
                throw new IndexOutOfRangeException();
            }
            else
            {
                // Vector not in memory or on disk -- this should never happen
                throw new FileNotFoundException();
            }
        }
    }

    /// <summary>
    /// Gets all vectors.
    /// WARNING: This method is memory and CPU intensive and should not be used in production.
    /// </summary>
    /// <returns>All Vector objects in a List</returns>
    public List<Vector> GetAllVectors()
    {
        // Added for support in Semantic Kernel's Memory Store.
        // It's a bad idea to call this on a production server.
        return _vectorList.ToList();
    }

    public void Insert(int index, Neighborly.Vector item)
    {
        if (index < 0 || index > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");
        }

        lock (_lock)
        {
            // Check if this item can be inserted in memory
            if (index < _maxInMemoryCount)
            {
                // There is room in the in-memory list
                _vectorList.Insert(index, item);
                _onDiskFilePaths.Insert(index, string.Empty); // Empty string indicates that the item is in memory
                _guids.Insert(index, item.Id);
            }
            else
            {
                // There is no room in the in-memory list
                // Store the item on disk

                var path = Path.GetTempFileName();

                _vectorList.Insert(index, null);    // null indicates that the item is on disk
                _onDiskFilePaths.Insert(index, path);
                _guids.Insert(index, item.Id);

                SaveToDisk(item, path);
 
            }
         
        }
        Modified?.Invoke(this, EventArgs.Empty);

    }

    public int IndexOf(Neighborly.Vector item)
    {
        lock (_lock)
        {
            int index = _vectorList.IndexOf(item);
            if (index != -1)
            {
                return index;
            }

            // Vector not found in memory, check on disk
            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var path = _onDiskFilePaths[i];
                var vectorFromDisk = ReadFromDisk(path);
                if (EqualityComparer<Neighborly.Vector>.Default.Equals(vectorFromDisk, item))
                {
                    return _vectorList.Count + i;
                }
            }

            return -1;
        }
    }
    public bool IsReadOnly
    {
        get { return false; } // This list is not read-only
    }
    public List<Neighborly.Vector> FindAll(Predicate<Neighborly.Vector> match)
    {
        lock (_lock)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match), "Match predicate cannot be null");
            }

            var foundItems = new List<Neighborly.Vector>();

            foreach (var item in _vectorList)
            {
                if (item != null && match(item))
                {
                    foundItems.Add(item);
                }
            }

            foreach (var path in _onDiskFilePaths)
            {
                Neighborly.Vector diskItem = ReadFromDisk(path);
                if (path != string.Empty && match(diskItem))
                {
                    foundItems.Add(diskItem);
                }
            }

            return foundItems;
        }
    }

    public Neighborly.Vector Find(Predicate<Neighborly.Vector> match)
    {
        lock (_lock)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match), "Match predicate cannot be null");
            }

            foreach (var item in _vectorList)
            {
                if (item != null && match(item))
                {
                    return item;
                }
            }

            foreach (var path in _onDiskFilePaths)
            {
                Neighborly.Vector diskItem = ReadFromDisk(path);
                if (path != string.Empty && match(diskItem))
                {
                    return diskItem;
                }
            }

            return default(Neighborly.Vector);
        }
    }

    public void CopyTo(Neighborly.Vector[] array, int arrayIndex)
    {
        lock (_lock)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array), "Array cannot be null");
            }

            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Array index cannot be less than 0");
            }

            if (array.Length - arrayIndex < Count)
            {
                throw new ArgumentException("The number of elements in the list is greater than the available space from arrayIndex to the end of the destination array");
            }

            foreach (var item in _vectorList)
            {
                array[arrayIndex++] = item;
            }

            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var path = _onDiskFilePaths[i];
                Neighborly.Vector item = ReadFromDisk(path);
                array[arrayIndex++] = item;
            }
        }
    }

    public Neighborly.Vector this[int index]
    {
        get
        {
            lock (_lock)
            {
                return Get(index);
            }
        }
        set
        {
            lock (_lock)
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }

                if (index < _maxInMemoryCount)
                {
                    _vectorList[index] = value;
                }
                else
                {
                    var path = _onDiskFilePaths[index];
                    if (path == string.Empty)
                    {
                        path = Path.GetTempFileName();
                        _onDiskFilePaths[index] = path;
                    }
                    SaveToDisk(value, path);
                }
            }
            Modified?.Invoke(this, EventArgs.Empty);

        }
    }


    public bool Contains(Neighborly.Vector item)
    {
        lock (_lock)
        {
            if (_vectorList.Contains(item))
            {
                return true;
            }

            foreach (var path in _onDiskFilePaths)
            {
                if (path == string.Empty) continue;

                var diskItem = ReadFromDisk(path);
                if (EqualityComparer<Neighborly.Vector>.Default.Equals(diskItem, item))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public IEnumerator<Neighborly.Vector> GetEnumerator()
    {
        lock (_lock)
        {
            for (int i = 0; i < _vectorList.Count; i++)
            {
                if (_vectorList[i] != null)
                    yield return _vectorList[i];
                else if (_onDiskFilePaths[i] != string.Empty)
                    yield return ReadFromDisk(_onDiskFilePaths[i]);
                else
                    // This should never happen
                    throw new FileNotFoundException();
            }
        }

    }
    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");
            }

            _vectorList.RemoveAt(index);

            // Remove the item from disk if it exists
            if (_onDiskFilePaths[index] != string.Empty)
                File.Delete(_onDiskFilePaths[index]);

            _onDiskFilePaths.RemoveAt(index);

        }
        Modified?.Invoke(this, EventArgs.Empty);

    }



    public bool Remove(Neighborly.Vector item)
    {
        var index = _vectorList.FindIndex(x => x == item);
        RemoveAt(index);
        return true;
    }

    public int Count => _vectorList.Count + _onDiskFilePaths.Count;


    public void Clear()
    {
        lock (_lock)
        {
            _vectorList.Clear();
            foreach (var path in _onDiskFilePaths)
            {
                if (path != string.Empty && File.Exists(path))
                    File.Delete(path);
            }
            _onDiskFilePaths.Clear();
            _guids.Clear();
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void SaveToDisk(Neighborly.Vector v, string path)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(v.ToBinary());
        }
    }

    private Neighborly.Vector ReadFromDisk(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(stream))
        {
            return new Neighborly.Vector(reader);
        }
    }

    /// <summary>
    /// Swaps an item from memory to disk.
    /// </summary>
    /// <param name="index"></param>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public void MoveToDisk(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _vectorList.Count)
            {
                throw new IndexOutOfRangeException();
            }

            // Check if the item is already on disk. This is evident if the item is null.
            if (_vectorList[index] == null)
                return;

            Neighborly.Vector v = _vectorList[index];
            var path = Path.GetTempFileName();
            SaveToDisk(v, path);
            _onDiskFilePaths[index] = path;
            _vectorList[index] = null;
        }
    }

    public void MoveToMemory(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _onDiskFilePaths.Count)
            {
                throw new IndexOutOfRangeException();
            }

            // Check if the item is already in memory. This is evident if the item is not null.
            if (_vectorList[index] != null)
                return;

            var path = _onDiskFilePaths[index];
            _vectorList[index] = ReadFromDisk(path);
            File.Delete(path);
            _onDiskFilePaths[index] = string.Empty;
        }
    }

    public int FindIndexById(Guid id)
    {
        return _guids.FindIndex(x => x == id);
    }
    public int FindIndex(Predicate<Neighborly.Vector> match)
    {
        lock (_lock)
        {
            for (int i = 0; i < _vectorList.Count; i++)
            {
                if (match(_vectorList[i]))
                {
                    return i;
                }
            }

            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var path = _onDiskFilePaths[i];
                var diskItem = ReadFromDisk(path);
                if (match(diskItem))
                {
                    return _vectorList.Count + i;
                }
            }

            return -1; // Return -1 if no match is found
        }
    }

    public void RemoveRange(IEnumerable<Neighborly.Vector> items)
    {
        lock (_lock)
        {
            foreach (var item in items)
            {
                Remove(item);
            }
        }
    }

    public bool Update(Neighborly.Vector vector)
    {
        lock (_lock)
        {
            int index = IndexOf(vector);
            if (index != -1)
            {
                if (_vectorList[index] != null)
                {
                    _vectorList[index] = vector;
                }
                else if (_onDiskFilePaths[index] != string.Empty)
                {
                    SaveToDisk(vector, _onDiskFilePaths[index]);
                }
                else
                {
                    // This should never happen
                    throw new FileNotFoundException();
                }
            }
            else
            {
                throw new ArgumentException("Item not found in list");
            }
            Modified?.Invoke(this, EventArgs.Empty);
            return true;
        }

    }

    public void RemoveById(Guid guid)
    {
        lock (_lock)
        {
            int index = FindIndexById(guid);
            if (index != -1)
            {
                RemoveAt(index);
            }
        }

    }

}