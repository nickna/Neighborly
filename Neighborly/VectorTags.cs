using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Neighborly;

/// <summary>
/// VectorTags are simple strings (tags) that can be associated with a vector.
/// Each tag is assigned a unique id (short) which can be used to identify the tag.
/// The tags are case-insensitive and are stored in lower case.
/// Thread-safe implementation using ReaderWriterLockSlim for read-heavy scenarios.
/// </summary>
public class VectorTags : IDisposable
{
    // Core tag storage - protected by _rwLock
    private readonly Dictionary<short, string> _tags = new();
    private readonly Dictionary<string, short> _tagsByName = new(StringComparer.OrdinalIgnoreCase);

    // Tag-to-Vector mapping - uses volatile + atomic swap pattern for lock-free reads
    private volatile IReadOnlyDictionary<short, IReadOnlyList<Guid>> _tagMap =
        new Dictionary<short, IReadOnlyList<Guid>>();

    private readonly VectorList _vectorList;
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);
    private bool _disposed;

    public event EventHandler? Modified;

    public VectorTags(VectorList vectorList)
    {
        _vectorList = vectorList ?? throw new ArgumentNullException(nameof(vectorList));
    }

    /// <summary>
    /// Gets the tag ID for the given tag string. O(1) lookup.
    /// </summary>
    /// <param name="tag">The tag name to look up</param>
    /// <returns>The tag ID, or -1 if not found</returns>
    public short GetId(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return -1;

        var normalizedTag = tag.Trim().ToLowerInvariant();

        _rwLock.EnterReadLock();
        try
        {
            return _tagsByName.TryGetValue(normalizedTag, out var id) ? id : (short)-1;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Tries to get the tag ID for the given tag string. O(1) lookup.
    /// </summary>
    /// <param name="tag">The tag name to look up</param>
    /// <param name="tagId">The tag ID if found, -1 otherwise</param>
    /// <returns>True if found, false otherwise</returns>
    public bool TryGetId(string tag, out short tagId)
    {
        tagId = -1;
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var normalizedTag = tag.Trim().ToLowerInvariant();

        _rwLock.EnterReadLock();
        try
        {
            if (_tagsByName.TryGetValue(normalizedTag, out var id))
            {
                tagId = id;
                return true;
            }
            return false;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the tag IDs for multiple tag strings.
    /// </summary>
    /// <param name="tags">The tag names to look up</param>
    /// <returns>Array of tag IDs (-1 for tags not found)</returns>
    public short[] GetIdRange(string[] tags)
    {
        if (tags == null || tags.Length == 0)
            return Array.Empty<short>();

        _rwLock.EnterReadLock();
        try
        {
            var result = new short[tags.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                var normalizedTag = tags[i]?.Trim().ToLowerInvariant() ?? string.Empty;
                result[i] = _tagsByName.TryGetValue(normalizedTag, out var id) ? id : (short)-1;
            }
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the tag names for the given tag IDs.
    /// </summary>
    /// <param name="tagIds">The tag IDs to look up</param>
    /// <returns>Array of tag names</returns>
    /// <exception cref="KeyNotFoundException">Thrown if a tag ID is not found</exception>
    public string[] GetRange(short[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            return Array.Empty<string>();

        _rwLock.EnterReadLock();
        try
        {
            var result = new string[tagIds.Length];
            for (int i = 0; i < tagIds.Length; i++)
            {
                result[i] = _tags[tagIds[i]];
            }
            return result;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a new tag or returns the ID of an existing tag. Thread-safe.
    /// </summary>
    /// <param name="tag">The tag name to add</param>
    /// <returns>The tag ID</returns>
    /// <exception cref="ArgumentException">Thrown if tag is null or whitespace</exception>
    /// <exception cref="InvalidOperationException">Thrown if maximum number of tags reached</exception>
    public short Add(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be null or whitespace", nameof(tag));

        var normalizedTag = tag.Trim().ToLowerInvariant();
        bool added = false;
        short tagId;

        _rwLock.EnterUpgradeableReadLock();
        try
        {
            // Check if tag already exists
            if (_tagsByName.TryGetValue(normalizedTag, out var existingId))
            {
                return existingId;
            }

            // Need to add - upgrade to write lock
            _rwLock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_tagsByName.TryGetValue(normalizedTag, out existingId))
                {
                    return existingId;
                }

                if (_tags.Count >= short.MaxValue)
                {
                    throw new InvalidOperationException("Maximum number of tags reached");
                }

                tagId = (short)(_tags.Count + 1);
                _tags.Add(tagId, normalizedTag);
                _tagsByName.Add(normalizedTag, tagId);
                added = true;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
        finally
        {
            _rwLock.ExitUpgradeableReadLock();
        }

        // Raise event outside of lock to prevent deadlocks
        if (added)
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }

        return tagId;
    }

    /// <summary>
    /// Removes a tag by ID.
    /// Note: Does NOT remove tag references from vectors - call BuildMap() to refresh.
    /// </summary>
    /// <param name="tagId">The ID of the tag to remove</param>
    /// <returns>True if the tag was removed, false if not found</returns>
    public bool Remove(short tagId)
    {
        bool removed = false;

        _rwLock.EnterWriteLock();
        try
        {
            if (_tags.TryGetValue(tagId, out var tagName))
            {
                _tags.Remove(tagId);
                _tagsByName.Remove(tagName);
                removed = true;
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        if (removed)
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>
    /// Removes a tag by name.
    /// Note: Does NOT remove tag references from vectors - call BuildMap() to refresh.
    /// </summary>
    /// <param name="tag">The name of the tag to remove</param>
    /// <returns>True if the tag was removed, false if not found</returns>
    public bool Remove(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var normalizedTag = tag.Trim().ToLowerInvariant();
        bool removed = false;

        _rwLock.EnterWriteLock();
        try
        {
            if (_tagsByName.TryGetValue(normalizedTag, out var tagId))
            {
                _tags.Remove(tagId);
                _tagsByName.Remove(normalizedTag);
                removed = true;
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        if (removed)
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>
    /// Clears all tags.
    /// </summary>
    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _tags.Clear();
            _tagsByName.Clear();
            _tagMap = new Dictionary<short, IReadOnlyList<Guid>>();
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the number of tags.
    /// </summary>
    public int Count
    {
        get
        {
            _rwLock.EnterReadLock();
            try
            {
                return _tags.Count;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Checks if a tag exists. O(1) lookup.
    /// </summary>
    /// <param name="tag">The tag name to check</param>
    /// <returns>True if the tag exists</returns>
    public bool Contains(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var normalizedTag = tag.Trim().ToLowerInvariant();

        _rwLock.EnterReadLock();
        try
        {
            return _tagsByName.ContainsKey(normalizedTag);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns the tag string for the given tag ID.
    /// </summary>
    /// <param name="tagId">The tag ID</param>
    /// <returns>The tag name</returns>
    /// <exception cref="KeyNotFoundException">Thrown if tag ID not found</exception>
    public string this[short tagId]
    {
        get
        {
            _rwLock.EnterReadLock();
            try
            {
                return _tags[tagId];
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Returns the tag ID for the given tag string.
    /// </summary>
    /// <param name="tag">The tag name</param>
    /// <returns>The tag ID, or -1 if not found</returns>
    public short this[string tag]
    {
        get => GetId(tag);
    }

    #region Tag-to-Vector Query Methods

    /// <summary>
    /// Gets all vector IDs that have the specified tag.
    /// Lock-free read using volatile reference.
    /// </summary>
    /// <param name="tagId">The tag ID to search for</param>
    /// <returns>Read-only list of vector GUIDs, or empty if tag not found</returns>
    public IReadOnlyList<Guid> GetVectorIdsByTag(short tagId)
    {
        var map = _tagMap; // Volatile read - safe snapshot
        return map.TryGetValue(tagId, out var ids) ? ids : Array.Empty<Guid>();
    }

    /// <summary>
    /// Gets vector IDs that have ALL of the specified tags (intersection).
    /// </summary>
    /// <param name="tagIds">Array of tag IDs - all must be present</param>
    /// <returns>List of vector GUIDs that have all specified tags</returns>
    public IReadOnlyList<Guid> GetVectorIdsByTags(short[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            return Array.Empty<Guid>();

        var map = _tagMap; // Volatile read

        // Find smallest set first for efficiency
        IReadOnlyList<Guid>? smallestSet = null;
        int smallestCount = int.MaxValue;

        foreach (var tagId in tagIds)
        {
            if (!map.TryGetValue(tagId, out var ids) || ids.Count == 0)
                return Array.Empty<Guid>(); // Tag not found or empty, no intersection possible

            if (ids.Count < smallestCount)
            {
                smallestCount = ids.Count;
                smallestSet = ids;
            }
        }

        if (smallestSet == null)
            return Array.Empty<Guid>();

        // Start with smallest set and intersect with others
        var result = new HashSet<Guid>(smallestSet);

        foreach (var tagId in tagIds)
        {
            if (map.TryGetValue(tagId, out var ids))
            {
                result.IntersectWith(ids);

                if (result.Count == 0)
                    return Array.Empty<Guid>();
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Gets vector IDs that have ANY of the specified tags (union).
    /// </summary>
    /// <param name="tagIds">Array of tag IDs - any can be present</param>
    /// <returns>List of vector GUIDs that have at least one specified tag</returns>
    public IReadOnlyList<Guid> GetVectorIdsByAnyTag(short[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            return Array.Empty<Guid>();

        var map = _tagMap; // Volatile read
        var result = new HashSet<Guid>();

        foreach (var tagId in tagIds)
        {
            if (map.TryGetValue(tagId, out var ids))
            {
                result.UnionWith(ids);
            }
        }

        return result.ToArray();
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Convert tags to binary data using efficient buffer allocation.
    /// Format: [tagId:short][nameLength:int][nameBytes:UTF8]...
    /// </summary>
    /// <returns>Binary representation of tags</returns>
    public byte[] ToBinary()
    {
        _rwLock.EnterReadLock();
        try
        {
            if (_tags.Count == 0)
            {
                return Array.Empty<byte>();
            }

            // Calculate required size
            int totalSize = 0;
            foreach (var tag in _tags)
            {
                totalSize += sizeof(short); // tagId
                totalSize += sizeof(int);   // nameLength
                totalSize += Encoding.UTF8.GetByteCount(tag.Value);
            }

            // Allocate buffer and write
            var buffer = new byte[totalSize];
            int offset = 0;

            foreach (var tag in _tags)
            {
                // Write tag ID
                BitConverter.TryWriteBytes(buffer.AsSpan(offset), tag.Key);
                offset += sizeof(short);

                // Write name length and bytes
                var nameBytes = Encoding.UTF8.GetBytes(tag.Value);
                BitConverter.TryWriteBytes(buffer.AsSpan(offset), nameBytes.Length);
                offset += sizeof(int);

                nameBytes.CopyTo(buffer, offset);
                offset += nameBytes.Length;
            }

            return buffer;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Deserialize binary data to tags.
    /// </summary>
    /// <param name="data">Binary data to deserialize</param>
    public void FromBinary(byte[] data)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _tags.Clear();
            _tagsByName.Clear();

            if (data == null || data.Length == 0)
                return;

            int offset = 0;
            while (offset < data.Length)
            {
                short tagId = BitConverter.ToInt16(data, offset);
                offset += sizeof(short);

                int tagLength = BitConverter.ToInt32(data, offset);
                offset += sizeof(int);

                string tag = Encoding.UTF8.GetString(data, offset, tagLength);
                offset += tagLength;

                _tags[tagId] = tag;
                _tagsByName[tag] = tagId;
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    #endregion

    /// <summary>
    /// Get a text representation of the tags.
    /// </summary>
    /// <param name="tagIds">The tag IDs to convert</param>
    /// <returns>Comma-separated tag names</returns>
    public string GetRangeAsString(short[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
        {
            return string.Empty;
        }

        _rwLock.EnterReadLock();
        try
        {
            var tags = new List<string>(tagIds.Length);
            foreach (var tagId in tagIds)
            {
                if (_tags.TryGetValue(tagId, out var tagName))
                {
                    tags.Add(tagName);
                }
            }
            return string.Join(", ", tags);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Creates a tag map for the vectors using atomic swap pattern.
    /// Thread-safe: builds new map without holding lock, then swaps atomically.
    /// </summary>
    /// <param name="snapshot">Optional pre-created snapshot of vectors for thread-safe iteration.
    /// If null, a snapshot will be created from the vector list.</param>
    public void BuildMap(IReadOnlyList<Vector>? snapshot = null)
    {
        var vectors = snapshot ?? _vectorList.ToList();

        if (vectors.Count == 0)
        {
            // Atomic swap to empty map
            _tagMap = new Dictionary<short, IReadOnlyList<Guid>>();
            return;
        }

        // Build new map without holding lock - use HashSet for O(1) add and deduplication
        var newTagMap = new Dictionary<short, HashSet<Guid>>();

        foreach (var vector in vectors)
        {
            foreach (var tagId in vector.Tags)
            {
                if (!newTagMap.TryGetValue(tagId, out var set))
                {
                    set = new HashSet<Guid>();
                    newTagMap[tagId] = set;
                }
                set.Add(vector.Id);
            }
        }

        // Convert to read-only lists for the final immutable map
        var finalMap = new Dictionary<short, IReadOnlyList<Guid>>(newTagMap.Count);
        foreach (var kvp in newTagMap)
        {
            finalMap[kvp.Key] = kvp.Value.ToArray();
        }

        // Atomic swap (volatile write)
        _tagMap = finalMap;
    }

    /// <summary>
    /// Returns all tags in their human-readable format.
    /// </summary>
    /// <returns>Read-only list of tag names</returns>
    public IReadOnlyList<string> GetAllTags()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _tags.Values.ToArray();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Asynchronously enumerates all tags.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of tag names</returns>
    public async IAsyncEnumerable<string> GetAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var snapshot = GetAllTags();

        foreach (var tag in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return tag;
            await Task.Yield();
        }
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _rwLock.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
