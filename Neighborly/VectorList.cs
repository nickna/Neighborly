using CsvHelper;
using System.Collections;

namespace Neighborly;

/// <summary>
/// List that stores items in memory up to a certain count, then spills over to disk.
/// </summary>
public class VectorList : IList<Vector>, IDataPersistence, IDisposable
{
    private readonly VectorTags _tags;
    public VectorTags Tags => _tags;
    private readonly MemoryMappedList _memoryMappedList;
    private bool _disposed = false;

    /// <summary>
    /// Event that is triggered when data has changed
    /// </summary>
    public event EventHandler? Modified;



    public VectorList(BinaryReader reader)
    {
        _memoryMappedList = new(capacity: Int16.MaxValue);
        
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            Add(new Vector(reader));
        }

        _tags = new VectorTags(reader, this);

        // VectorList.Modified event is triggered when VectorTags.Modified event is triggered
        _tags.Modified += (sender, e) => Modified?.Invoke(this, EventArgs.Empty);
    }

    public VectorList(string? basePath = null, string? dbTitle = null, FileMode fileMode = FileMode.OpenOrCreate)
    {       
        _memoryMappedList = new MemoryMappedList(
            capacity: Int16.MaxValue, 
            baseFilePath: basePath,
            fileTitle: dbTitle, 
            fileMode: fileMode);

        _tags = new VectorTags(this);

        // VectorList.Modified event is triggered when VectorTags.Modified event is triggered
        _tags.Modified += (sender, e) => Modified?.Invoke(this, EventArgs.Empty);
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

    public void Add(Vector item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _memoryMappedList.Add(item);

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
        return _memoryMappedList.GetVector(index);
    }


    public void Insert(int index, Vector item) => throw new NotSupportedException("Inserting items at a specific index is not supported");

    public int IndexOf(Vector item)
    {
        // Vector not found in list, check in memory mapped list
        long memoryMappedIndex = _memoryMappedList.IndexOf(item);
        if (memoryMappedIndex != -1)
        {
            // TOOD: Fix cast with https://github.com/nickna/Neighborly/issues/33
            return (int)(memoryMappedIndex);
        }

        return -1;
    }
    public bool IsReadOnly { get; private set; }
    public List<Vector> FindAll(Predicate<Vector> match)
    {
        if (match == null)
        {
            throw new ArgumentNullException(nameof(match), "Match predicate cannot be null");
        }

        var foundItems = new List<Vector>();

        foundItems.AddRange(_memoryMappedList.Where(x => x != null && match(x)).Select(x => x!));

        return foundItems;
    }

    public Vector? Find(Predicate<Vector> match)
    {
        if (match == null)
        {
            throw new ArgumentNullException(nameof(match), "Match predicate cannot be null");
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

    public void CopyTo(Vector[] array, int arrayIndex)
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

        _memoryMappedList.CopyTo(array, arrayIndex);
    }

    public Vector this[int index]
    {
        get
        {
            return Get(index) ?? throw new IndexOutOfRangeException();
        }
        set
        {
            if (index < 0 || index >= Count)
            {
                throw new IndexOutOfRangeException();
            }

            throw new NotSupportedException("Updating items on disk is not supported");
        }
    }

    /// <summary>
    /// Checks the list for the presence of a vector by Id.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Contains(Vector item)
    {
        return _memoryMappedList.GetVector(item.Id) != null;
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        return _memoryMappedList.GetEnumerator();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range");
        }

        var vector = _memoryMappedList.GetVector(index);
        if (vector != null)
        {
            _memoryMappedList.Remove(vector);
        }

        Modified?.Invoke(this, EventArgs.Empty);

    }

    internal Vector? GetById(Guid id)
    {
        if (Guid.Empty.Equals(id))
        {
            // Return default if id is empty
            return default;
        }

        return _memoryMappedList.GetVector(id);
    }

    public bool Remove(Vector item)
    {
        return _memoryMappedList.Remove(item);
    }

    public int Count => (int)_memoryMappedList.Count;

    public void Clear()
    {
        _memoryMappedList.Clear();

        Modified?.Invoke(this, EventArgs.Empty);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int FindIndexById(Guid id)
    {
        // TOOD: Fix cast with https://github.com/nickna/Neighborly/issues/33
        return (int)_memoryMappedList.FindIndexById(id);
    }

    public int FindIndex(Predicate<Vector> match)
    {
        var index = 0;

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

    public void RemoveRange(IEnumerable<Vector> items)
    {
        foreach (var item in items)
        {
            Remove(item);
        }
    }

    public bool Update(Guid Id, Vector vector)
    {
        vector.Id = Id; // Change the Id of the incoming vector to match the Id of the existing vector

        var updated = _memoryMappedList.Update(vector);

        if (updated)
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }

        return updated;
    }

    public void RemoveById(Guid guid)
    {
        int index = FindIndexById(guid);
        if (index != -1)
        {
            RemoveAt(index);
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

    internal long[] GetFileInfo()
    {
        return _memoryMappedList.GetFileInfo();
    }

    internal void ForceFlush()
    {
        _memoryMappedList.Flush();
        return;
    }

    public void ToBinaryStream(BinaryWriter writer)
    {
        writer.Write(Count);
        foreach (var vector in _memoryMappedList)
        {
            vector.ToBinaryStream(writer);
        }

        _tags.ToBinaryStream(writer);
    }
}