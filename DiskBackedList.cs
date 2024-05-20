using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class DiskBackedList<T>
{
    private List<T> _inMemoryItems = new List<T>();
    private List<string> _onDiskFilePaths = new List<string>();
    private int _maxInMemoryCount;

    public DiskBackedList(int maxInMemoryCount)
    {
        _maxInMemoryCount = maxInMemoryCount;
    }

    public void Add(T item)
    {
        if (_inMemoryItems.Count < _maxInMemoryCount)
        {
            _inMemoryItems.Add(item);
        }
        else
        {
            var formatter = new BinaryFormatter();
            var path = Path.GetTempFileName();
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                formatter.Serialize(stream, item);
            }
            _onDiskFilePaths.Add(path);
        }
    }

    public T Get(int index)
    {
        if (index < _inMemoryItems.Count)
        {
            return _inMemoryItems[index];
        }
        else
        {
            var formatter = new BinaryFormatter();
            var path = _onDiskFilePaths[index - _inMemoryItems.Count];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return (T)formatter.Deserialize(stream);
            }
        }
    }

    public class DiskBackedList<T> : IEnumerable<T>
    {

        public IEnumerator<T> GetEnumerator()
        {
            // First, yield return all in-memory items
            foreach (var item in _inMemoryItems)
            {
                yield return item;
            }

            // Then, yield return all on-disk items
            foreach (var path in _onDiskFilePaths)
            {
                var formatter = new BinaryFormatter();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    yield return (T)formatter.Deserialize(stream);
                }
            }
        }

        // Explicit interface implementation for non-generic IEnumerable
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public bool Remove(T item)
    {
        // First, try to remove from in-memory items
        bool removed = _inMemoryItems.Remove(item);

        // If not removed from in-memory items, try to remove from disk
        if (!removed)
        {
            for (int i = 0; i < _onDiskFilePaths.Count; i++)
            {
                var formatter = new BinaryFormatter();
                var path = _onDiskFilePaths[i];
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    T diskItem = (T)formatter.Deserialize(stream);
                    if (EqualityComparer<T>.Default.Equals(diskItem, item))
                    {
                        // Delete the file
                        File.Delete(path);
                        // Remove the file path from the list
                        _onDiskFilePaths.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }
        }

        return removed;
    }

}
