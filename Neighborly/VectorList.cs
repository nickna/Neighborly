using System.Collections;

namespace Neighborly;

/// <summary>
/// List that stores items in memory up to a certain count, then spills over to disk.
/// </summary>
public class VectorList : IList<Vector>, IDisposable
{
    private readonly List<Vector?> _vectorList;
    private readonly VectorTags _tags;
    public VectorTags Tags => _tags;
    private readonly int _maxInMemoryCount;
    private readonly MemoryMappedList _memoryMappedList = new(int.MaxValue);
    private readonly object _lock = new();
    private bool _disposed = false;

    /// <summary>
    /// Event that is triggered when data has changed
    /// </summary>
    public event EventHandler? Modified;

    /// <summary>
    /// Creates a new instance of DiskBackedList with a maximum in-memory count based on system memory.
    /// </summary>
    public VectorList()
    {
        _tags = new VectorTags(this);
        // VectorList.Modified event is triggered when VectorTags.Modified event is triggered
        _tags.Modified += (sender, e) => Modified?.Invoke(this, EventArgs.Empty);
        _vectorList = [];
    }

    /// <summary>
    /// Creates a new instance and Sets the maximum in-memory count.
    /// </summary>
    /// <param name="maxInMemoryCount"></param>
    public VectorList(int maxInMemoryCount) : this()
    {
        _maxInMemoryCount = maxInMemoryCount;
        _vectorList = new(_maxInMemoryCount);
    }

    /// <summary>
    /// Defragments the MemoryMappedList if the fragmentation is greater than the threshold.
    /// (Threshold is currently set to 0)
    /// </summary>
    public void Defrag()
    {
       _memoryMappedList.Defrag();
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
                IsReadOnly = true;

                // Dispose managed resources.
                _vectorList.Clear();
                _memoryMappedList.Dispose();
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

    public void Add(Vector item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_lock)
        {
            if (_vectorList.Count + 1 > _maxInMemoryCount)
            {
                _memoryMappedList.Add(item);
            }
            else
            {
                _vectorList.Add(item);
            }
        }

        Modified?.Invoke(this, EventArgs.Empty);
    }

    public void AddRange(IEnumerable<Vector> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public Vector? Get(int index)
    {
        lock (_lock)
        {
            if (index < _maxInMemoryCount - 1 && _vectorList[index] != null)
            {
                // Vector is in memory
                return _vectorList[index]!;
            }

            return _memoryMappedList.GetVector(index - _maxInMemoryCount);
        }
    }


    public void Insert(int index, Vector item) => throw new NotSupportedException("Inserting items at a speficic index is not supported");

    public int IndexOf(Vector item)
    {
        lock (_lock)
        {
            int index = _vectorList.IndexOf(item);
            if (index != -1)
            {
                return index;
            }

            // Vector not found in list, check in memory mapped list
            long memoryMappedIndex = _memoryMappedList.IndexOf(item);
            if (memoryMappedIndex != -1)
            {
                // TOOD: Fix cast with https://github.com/nickna/Neighborly/issues/33
                return (int)(memoryMappedIndex + _maxInMemoryCount);
            }

            return -1;
        }
    }
    public bool IsReadOnly { get; private set; }
    public List<Vector> FindAll(Predicate<Vector> match)
    {
        lock (_lock)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match), "Match predicate cannot be null");
            }

            var foundItems = new List<Vector>();

            foundItems.AddRange(_vectorList.Where(x => x != null && match(x)).Select(x => x!));
            foundItems.AddRange(_memoryMappedList.Where(x => x != null && match(x)).Select(x => x!));

            return foundItems;
        }
    }

    public Vector? Find(Predicate<Vector> match)
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

            foreach (var item in _memoryMappedList)
            {
                if (item != null && match(item))
                {
                    return item;
                }
            }

            return default;
        }
    }

    public void CopyTo(Vector[] array, int arrayIndex)
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

            _vectorList.CopyTo(array, arrayIndex);
            _memoryMappedList.CopyTo(array, arrayIndex + _vectorList.Count);
        }
    }

    public Vector this[int index]
    {
        get
        {
            lock (_lock)
            {
                return Get(index) ?? throw new IndexOutOfRangeException();
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
                    throw new NotSupportedException("Updating items on disk is not supported");
                }
            }

            Modified?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Checks the list for the presence of a vector by Id.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Contains(Vector item)
    {
        lock (_lock)
        {
            if (_vectorList.Any(v => v != null && v.Id == item.Id))
            {
                return true;
            }

            return _memoryMappedList.GetVector(item.Id) != null;
        }
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        lock (_lock)
        {
            foreach (var vector in _vectorList.Where(v => v != null))
            {
                yield return vector!;
            }

            foreach (var vector in _memoryMappedList)
            {
                yield return vector;
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

            if (index < _maxInMemoryCount)
            {
                _vectorList.RemoveAt(index);
            }
            else
            {
                var vector = _memoryMappedList.GetVector(index - _maxInMemoryCount);
                if (vector != null)
                {
                    _memoryMappedList.Remove(vector);
                }
            }
        }

        Modified?.Invoke(this, EventArgs.Empty);

    }

    internal Vector? GetById(Guid id)
    {
        lock (_lock)
        {
            if (Guid.Empty.Equals(id))
            {
                // Return default if id is empty
                return default;
            }

            var index = FindIndexById(id);
            if (index != -1)
            {
                return Get(index);
            }

            return default;
        }
    }

    public bool Remove(Vector item)
    {
        var index = _vectorList.FindIndex(x => x != null && x.Equals(item));
        if (index > -1)
        {
            RemoveAt(index);
            return true;
        }

        return _memoryMappedList.Remove(item);
    }

    public int Count => _vectorList.Count(v => v != null) + (int)_memoryMappedList.Count;

    public void Clear()
    {
        lock (_lock)
        {
            _memoryMappedList.Clear();
            _vectorList.Clear();
        }

        Modified?.Invoke(this, EventArgs.Empty);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int FindIndexById(Guid id)
    {
        var index = _vectorList.FindIndex(x => x.Id == id);
        if (index != -1)
        {
            return index;
        }

        // TOOD: Fix cast with https://github.com/nickna/Neighborly/issues/33
        return (int)_memoryMappedList.FindIndexById(id);
    }

    public int FindIndex(Predicate<Vector> match)
    {
        lock (_lock)
        {
            var index = _vectorList.FindIndex(x => x != null && match(x));
            if (index != -1)
            {
                return index;
            }

            foreach (var item in _memoryMappedList)
            {
                ++index;
                if (item != null && match(item))
                {
                    return index;
                }
            }

            return -1; // Return -1 if no match is found
        }
    }

    public void RemoveRange(IEnumerable<Vector> items)
    {
        lock (_lock)
        {
            foreach (var item in items)
            {
                Remove(item);
            }
        }
    }

    public bool Update(Guid Id, Vector vector)
    {
        vector.Id = Id; // Change the Id of the incoming vector to match the Id of the existing vector

        lock (_lock)
        {
            int index = _vectorList.IndexOf(vector);
            var updated = false;
            if (index != -1)
            {
                _vectorList[index] = vector;
                updated = true;
            }
            else
            {
                updated = _memoryMappedList.Update(vector);
            }

            if (updated)
            {
                Modified?.Invoke(this, EventArgs.Empty);
            }

            return updated;
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

    public long CalculateFragmentation()
    {
        return _memoryMappedList.CalculateFragmentation();
    }

    public long DefragBatch()
    {
        return _memoryMappedList.DefragBatch();
    }
}