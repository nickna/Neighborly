using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace Neighborly;

/// <summary>
/// List that stores items in memory up to a certain count, then spills over to disk.
/// </summary>
/// <typeparam name="T"></typeparam>
public class DiskBackedList<T> : IList<T>
{
    private List<T> _inMemoryItems = new List<T>();
    private List<string> _onDiskFilePaths = new List<string>();
    private List<bool> _isInMemory = new List<bool>();
    private int _maxInMemoryCount;
    private readonly object _lock = new object();


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
    /// Creates a new instance and Sets the maximum in-memory count.
    /// </summary>
    /// <param name="maxInMemoryCount"></param>
    public DiskBackedList(int maxInMemoryCount)
    {
        _maxInMemoryCount = maxInMemoryCount;
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            if (_inMemoryItems.Count < _maxInMemoryCount)
            {
                _inMemoryItems.Add(item);
                _isInMemory.Add(true);
            }
            else
            {
                var path = Path.GetTempFileName();
                var bytes = HelperFunctions.SerializeToBinary(item);
                HelperFunctions.WriteToFile(path, bytes);
                _onDiskFilePaths.Add(path);
                _isInMemory.Add(false);
            }
        }
    }

    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public T Get(int index)
    {
        lock (_lock)
        {
            if (_isInMemory[index])
            {
                return _inMemoryItems[index];
            }
            else
            {
                var path = _onDiskFilePaths[index - _inMemoryItems.Count];
                var bytes = HelperFunctions.ReadFromFile(path);
                return HelperFunctions.DeserializeFromBinary<T>(bytes);
            }
        }
    }
    public void Insert(int index, T item)
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
                    _isInMemory.Insert(index, true);
                }
                else
                {
                    // There is no room in the in-memory list
                    // Move the last in-memory item to disk
                    var lastInMemoryItem = _inMemoryItems[_inMemoryItems.Count - 1];
                    var path = Path.GetTempFileName();
                    var bytes = HelperFunctions.SerializeToBinary(lastInMemoryItem);
                    HelperFunctions.WriteToFile(path, bytes);
                    _onDiskFilePaths.Insert(_inMemoryItems.Count - 1, path);
                    _isInMemory[_inMemoryItems.Count - 1] = false;

                    // Insert the new item in the in-memory list
                    _inMemoryItems[index] = item;
                }
            }
            else
            {
                // The index is in the on-disk list
                var path = Path.GetTempFileName();
                var bytes = HelperFunctions.SerializeToBinary(item);
                HelperFunctions.WriteToFile(path, bytes);
                _onDiskFilePaths.Insert(index - _inMemoryItems.Count, path);
                _isInMemory.Insert(index, false);
            }
        }
    }

    public int IndexOf(T item)
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
                var bytes = HelperFunctions.ReadFromFile(path);
                var diskItem = HelperFunctions.DeserializeFromBinary<T>(bytes);
                if (EqualityComparer<T>.Default.Equals(diskItem, item))
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
    public List<T> FindAll(Predicate<T> match)
    {
        lock (_lock)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match), "Match predicate cannot be null");
            }

            var foundItems = new List<T>();

            foreach (var item in _inMemoryItems)
            {
                if (match(item))
                {
                    foundItems.Add(item);
                }
            }

            foreach (var path in _onDiskFilePaths)
            {
                var bytes = HelperFunctions.ReadFromFile(path);
                var diskItem = HelperFunctions.DeserializeFromBinary<T>(bytes);
                if (match(diskItem))
                {
                    foundItems.Add(diskItem);
                }
            }

            return foundItems;
        }
    }

    public T Find(Predicate<T> match)
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
                var bytes = HelperFunctions.ReadFromFile(path);
                var diskItem = HelperFunctions.DeserializeFromBinary<T>(bytes);
                if (match(diskItem))
                {
                    return diskItem;
                }
            }

            return default(T);
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
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
                var bytes = HelperFunctions.ReadFromFile(path);
                var item = HelperFunctions.DeserializeFromBinary<T>(bytes);
                array[arrayIndex++] = item;
            }
        }
    }

    public T this[int index]
    {
        get { lock (_lock) { return Get(index); } }
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
                    var bytes = HelperFunctions.SerializeToBinary(value);
                    HelperFunctions.WriteToFile(path, bytes);
                }
            }
        }
    }

    public bool Contains(T item)
    {
        lock (_lock)
        {
            if (_inMemoryItems.Contains(item))
            {
                return true;
            }

            foreach (var path in _onDiskFilePaths)
            {
                var bytes = HelperFunctions.ReadFromFile(path);
                var diskItem = HelperFunctions.DeserializeFromBinary<T>(bytes);
                if (EqualityComparer<T>.Default.Equals(diskItem, item))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in _inMemoryItems)
        {
            yield return item;
        }

        foreach (var path in _onDiskFilePaths)
        {
            var bytes = HelperFunctions.ReadFromFile(path);
            var item = HelperFunctions.DeserializeFromBinary<T>(bytes);
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
                _isInMemory.RemoveAt(index);
            }
            else
            {
                // The index is in the on-disk list
                var path = _onDiskFilePaths[index - _inMemoryItems.Count];
                File.Delete(path);
                _onDiskFilePaths.RemoveAt(index - _inMemoryItems.Count);
                _isInMemory.RemoveAt(index);
            }
        }
    }

    private bool RemoveItem(T item)
    {
        bool removed = _inMemoryItems.Remove(item);

        if (!removed)
        {
            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var path = _onDiskFilePaths[i];
                var bytes = HelperFunctions.ReadFromFile(path);
                var diskItem = HelperFunctions.DeserializeFromBinary<T>(bytes);
                if (EqualityComparer<T>.Default.Equals(diskItem, item))
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

    public bool Remove(T item)
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
            _isInMemory.Clear();
        }
    }

    


    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
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
            if (!_isInMemory[index])
                return;

            var item = _inMemoryItems[index];
            var path = Path.GetTempFileName();
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                // Assuming T is a primitive type
                if (item is IConvertible)
                {
                    var bytes = Convert.ChangeType(item, typeof(byte[])) as byte[];
                    if (bytes != null)
                    {
                        writer.Write(bytes);
                    }
                }
                _onDiskFilePaths[index] = path;
                _inMemoryItems[index] = default(T); // Or some sentinel value
                _isInMemory[index] = false;
            }
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
            if (_isInMemory[index])
                return;

            var path = _onDiskFilePaths[index];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                // Assuming T is a primitive type
                if (typeof(T) == typeof(byte[]))
                {
                    var item = (T)(object)reader.ReadBytes((int)stream.Length);
                    _inMemoryItems[index] = item;
                    _onDiskFilePaths[index] = null; // Or some sentinel value
                    _isInMemory[index] = true;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported type");
                }
            }
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




}
