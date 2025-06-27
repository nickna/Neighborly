using System.Collections;
using System.Collections.Concurrent;

namespace Neighborly;

public class VectorList : IList<Vector>, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Vector> _vectors = new();
    private readonly VectorTags _tags;
    public VectorTags Tags => _tags;
    private bool _disposed = false;

    public event EventHandler? Modified;

    public VectorList()
    {
        _tags = new VectorTags(this);
        _tags.Modified += (sender, e) => Modified?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                IsReadOnly = true;
                _vectors.Clear();
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
        if (_vectors.TryAdd(item.Id, item))
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }
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
        return _vectors.Values.ToList().ElementAtOrDefault(index);
    }

    public void Insert(int index, Vector item) => throw new NotSupportedException();

    public int IndexOf(Vector item)
    {
        var i = 0;
        foreach (var vector in _vectors.Values.ToList())
        {
            if (vector.Equals(item))
            {
                return i;
            }
            i++;
        }
        return -1;
    }

    public bool IsReadOnly { get; private set; }

    public List<Vector> FindAll(Predicate<Vector> match)
    {
        return _vectors.Values.ToList().Where(v => match(v)).ToList();
    }

    public Vector? Find(Predicate<Vector> match)
    {
        return _vectors.Values.ToList().FirstOrDefault(v => match(v));
    }

    public void CopyTo(Vector[] array, int arrayIndex)
    {
        _vectors.Values.ToList().CopyTo(array, arrayIndex);
    }

    public Vector this[int index]
    {
        get => _vectors.Values.ToList().ElementAt(index);
        set => throw new NotSupportedException();
    }

    public bool Contains(Vector item)
    {
        return _vectors.ContainsKey(item.Id);
    }

    public IEnumerator<Vector> GetEnumerator() => _vectors.Values.ToList().GetEnumerator();

    public IReadOnlyList<Guid> GetIds() => _vectors.Keys.ToList().AsReadOnly();

    public void RemoveAt(int index)
    {
        var vector = Get(index);
        if (vector != null)
        {
            Remove(vector);
        }
    }

    internal Vector? GetById(Guid id)
    {
        _vectors.TryGetValue(id, out var vector);
        return vector;
    }

    public bool Remove(Vector item)
    {
        if (_vectors.TryRemove(item.Id, out _))
        {
            Modified?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    public int Count => _vectors.Count;

    public void Clear()
    {
        _vectors.Clear();
        Modified?.Invoke(this, EventArgs.Empty);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int FindIndexById(Guid id)
    {
        var i = 0;
        foreach (var key in _vectors.Keys.ToList())
        {
            if (key == id)
            {
                return i;
            }
            i++;
        }
        return -1;
    }

    public int FindIndex(Predicate<Vector> match)
    {
        var i = 0;
        foreach (var vector in _vectors.Values.ToList())
        {
            if (match(vector))
            {
                return i;
            }
            i++;
        }
        return -1;
    }

    public void RemoveRange(IEnumerable<Vector> items)
    {
        foreach (var item in items)
        {
            Remove(item);
        }
    }

    internal bool Update(Guid id, Vector vector)
    {
        while (_vectors.TryGetValue(id, out Vector? existingVector))
        {
            // Create a new vector with the same ID as the existing one but with updated data
            var updatedVector = new Vector(vector.Values, vector.OriginalText);
            updatedVector.Id = id; // Preserve the original ID
            
            // Attempt to update. If it fails, it means another thread modified it,
            // so we loop and try again with the new existing value.
            if (_vectors.TryUpdate(id, updatedVector, existingVector))
            {
                Modified?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
        // If TryGetValue returns false, the key doesn't exist.
        return false;
    }

    public void RemoveById(Guid guid)
    {
        if (_vectors.TryRemove(guid, out _))
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }
    }
}