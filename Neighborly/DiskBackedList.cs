using System.Collections;
using System.Text;
using System.Text.Json;
using Neighborly;
namespace Neighborly;

/// <summary>
/// List that stores items in memory up to a certain count, then spills over to disk.
/// </summary>
public class DiskBackedList<Vector> : IList<Neighborly.Vector>, IDisposable
{
    private List<Neighborly.Vector>? _inMemoryItems = new List<Neighborly.Vector>();
    private List<string> _onDiskFilePaths = new List<string>();
    private int _maxInMemoryCount;
    private readonly object _lock = new object();
    private bool _disposed = false;

    /// <summary>
    /// Creates a new instance of DiskBackedList with a maximum in-memory count based on system memory.
    /// </summary>
    public DiskBackedList()
    {
        // Get current system memory (available to .NET CLR's GC)
        var systemMemory = GC.GetTotalMemory(forceFullCollection: false);

        // Calculate _maxInMemoryCount based on system memory
        _maxInMemoryCount = (int)(systemMemory / 1024 / 1024); // Convert bytes to megabytes

        // Set a minimum value for _maxInMemoryCount
        if (_maxInMemoryCount < 100)
        {
            _maxInMemoryCount = 100;
        }
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
                _inMemoryItems?.Clear();
            }

            // Dispose unmanaged resources.
            foreach (var filePath in _onDiskFilePaths)
            {
                if (File.Exists(filePath))
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

    ~DiskBackedList()
    {
        Dispose(false);
    }

    /// <summary>
    /// Creates a new instance and Sets the maximum in-memory count.
    /// </summary>
    /// <param name="maxInMemoryCount"></param>
    public DiskBackedList(int maxInMemoryCount)
    {
        _maxInMemoryCount = maxInMemoryCount;
    }

    public void Add(Neighborly.Vector item)
    {
        lock (_lock)
        {
            _onDiskFilePaths.Add(Path.GetTempFileName());
            _inMemoryItems.Add(item);
            var itemIndex = _inMemoryItems.Count - 1;

            if (_inMemoryItems.Count >= _maxInMemoryCount)
            {
                MoveToDisk(itemIndex);
            }

        }
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
            if (_inMemoryItems[index] != null)
            {
                return _inMemoryItems[index];
            }
            else
            {
                return ReadFromDisk(_onDiskFilePaths[index]);
            }
        }
    }
    public void Insert(int index, Neighborly.Vector item)
    {
        lock (_lock)
        {
            if (index < 0 || index > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");
            }

            if (index < _inMemoryItems.Count)
            {
                // The index is in the in-memory list
                if (_inMemoryItems.Count < _maxInMemoryCount)
                {
                    // There is room in the in-memory list
                    _inMemoryItems.Insert(index, item);
                }
                else
                {
                    // There is no room in the in-memory list
                    // Move the last in-memory item to disk
                    var lastInMemoryItem = _inMemoryItems.Count - 1;
                    
                    MoveToDisk(lastInMemoryItem);

                    var path = Path.GetTempFileName();
                    _onDiskFilePaths.Insert(_inMemoryItems.Count - 1, path);

                    // Insert the new item in the in-memory list
                    _inMemoryItems[index] = item;
                }
            }
            else
            {
                // The index is in the on-disk list
                var path = Path.GetTempFileName();
                MoveToDisk(index);
                _onDiskFilePaths.Insert(index - _inMemoryItems.Count, path);
            }
        }
    }

    public int IndexOf(Neighborly.Vector item)
    {
        lock (_lock)
        {
            int index = _inMemoryItems.IndexOf(item);
            if (index != -1)
            {
                return index;
            }

            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var path = _onDiskFilePaths[i];
                var diskItem = ReadFromDisk(path);
                if (EqualityComparer<Neighborly.Vector>.Default.Equals(diskItem, item))
                {
                    return _inMemoryItems.Count + i;
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

            foreach (var item in _inMemoryItems)
            {
                if (match(item))
                {
                    foundItems.Add(item);
                }
            }

            foreach (var path in _onDiskFilePaths)
            {
                Neighborly.Vector diskItem = ReadFromDisk(path);
                if (match(diskItem))
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

            foreach (var item in _inMemoryItems)
            {
                if (match(item))
                {
                    return item;
                }
            }

            foreach (var path in _onDiskFilePaths)
            {
                Neighborly.Vector diskItem = ReadFromDisk(path);
                if (match(diskItem))
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

            foreach (var item in _inMemoryItems)
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

                if (index < _inMemoryItems.Count)
                {
                    _inMemoryItems[index] = value;
                }
                else
                {
                    var path = _onDiskFilePaths[index - _inMemoryItems.Count];
                    using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(value.ToBinary());
                    }
                }
            }
        }
    }


    public bool Contains(Neighborly.Vector item)
    {
        lock (_lock)
        {
            if (_inMemoryItems.Contains(item))
            {
                return true;
            }

            foreach (var path in _onDiskFilePaths)
            {
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
        foreach (var item in _inMemoryItems)
        {
            yield return item;
        }

        foreach (var path in _onDiskFilePaths)
        {
            var item = ReadFromDisk(path);
            yield return item;
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

            if (index < _inMemoryItems.Count)
            {
                // The index is in the in-memory list
                _inMemoryItems.RemoveAt(index);
            }
            else
            {
                // The index is in the on-disk list
                var path = _onDiskFilePaths[index - _inMemoryItems.Count];
                File.Delete(path);
                _onDiskFilePaths.RemoveAt(index - _inMemoryItems.Count);
            }
        }
    }

    private bool RemoveItem(Neighborly.Vector item)
    {
        bool removed = _inMemoryItems.Remove(item);

        if (!removed)
        {
            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var path = _onDiskFilePaths[i];
                var diskItem = ReadFromDisk(path);
                if (EqualityComparer<Neighborly.Vector>.Default.Equals(diskItem, item))
                {
                    File.Delete(path);
                    _onDiskFilePaths.RemoveAt(i);
                    removed = true;
                    break;
                }
            }
        }

        return removed;
    }

    public bool Remove(Neighborly.Vector item)
    {
        lock (_lock)
        {
            return RemoveItem(item);
        }
    }

    public int Count => _inMemoryItems.Count + _onDiskFilePaths.Count;


    public void Clear()
    {
        lock (_lock)
        {
            _inMemoryItems.Clear();
            foreach (var path in _onDiskFilePaths)
            {
                File.Delete(path);
            }
            _onDiskFilePaths.Clear();
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
            if (index < 0 || index >= _inMemoryItems.Count)
            {
                throw new IndexOutOfRangeException();
            }

            // Check if the item is already on disk. This is evident if the item is null.
            if (_inMemoryItems[index] == null)
                return;

            Neighborly.Vector v = _inMemoryItems[index];
            var path = Path.GetTempFileName();
            SaveToDisk(v, path);
            _onDiskFilePaths[index] = path;
            _inMemoryItems[index] = null;
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
            if (_inMemoryItems[index] != null)
                return;

            var path = _onDiskFilePaths[index];
            _inMemoryItems[index] = ReadFromDisk(path);
            File.Delete(path);
        }
    }


    /// <summary>
    /// Shows the memory and disk size statistics of the list.
    /// </summary>
    /// <returns>Memory and disk consumption (in bytes)</returns>
    public (long memorySize, long diskSize) GetSizeStatistics()
    {
        lock (_lock)
        {
            long memorySize = 0;
            foreach (var item in _inMemoryItems)
            {
                var json = JsonSerializer.Serialize(item);
                memorySize += Encoding.UTF8.GetByteCount(json);
            }

            long diskSize = 0;
            foreach (var path in _onDiskFilePaths)
            {
                var fileInfo = new FileInfo(path);
                diskSize += fileInfo.Length;
            }

            return (memorySize, diskSize);
        }
    }

    public int FindIndex(Predicate<Neighborly.Vector> match)
    {
        lock (_lock)
        {
            for (int i = 0; i < _inMemoryItems.Count; i++)
            {
                if (match(_inMemoryItems[i]))
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
                    return _inMemoryItems.Count + i;
                }
            }

            return -1; // Return -1 if no match is found
        }
    }

}
