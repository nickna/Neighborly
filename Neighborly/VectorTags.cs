using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    /// <summary>
    /// VectorTags are simple strings (tags) that can be associated with a vector.
    /// Each tag is assigned a unique id (short) which can be used to identify the tag.
    /// The tags are case-insensitive and are stored in lower case.
    /// </summary>
    public class VectorTags 
    {
        private Dictionary<short, string> _tags = new();
        private Dictionary<short, List<Guid>> _tagMap = new();
        private VectorList _vectorList;
        public event EventHandler? Modified;

        public VectorTags(VectorList vectorList)
        {
            this._vectorList = vectorList;
        }

        public short GetId(string tag)
        {
            lock (_tags)
            {
                var key = _tags.FirstOrDefault(pair => pair.Value == tag.Trim().ToLower()).Key;
                if (key != default(short))
                {
                    return key;
                }
                else
                    return -1;
            }
        }

        public short[] GetIdRange(string[] tags)
        {
            lock (_tags)
            {
                return tags.Select(tag => GetId(tag)).ToArray();
            }
        }

        public string[] GetRange(short[] tagIds)
        {
            lock (_tags)
            {
                return tagIds.Select(tagId => _tags[tagId]).ToArray();
            }
        }

        public short Add(string tag)
        {
            if ( _tags.Count >= short.MaxValue)
            {
                throw new InvalidOperationException("Maximum number of tags reached");
            }

            if (_tags.ContainsValue(tag.Trim().ToLower()))
            {
                return GetId(tag);
            }

            lock(_tags)
            {
                short newTagId = (short)(_tags.Count + 1);
                _tags.Add(newTagId, tag.Trim().ToLower());
                Modified?.Invoke(this, EventArgs.Empty);
                return newTagId;
            }
        }

        public void Clear()
        {
            lock (_tags)
            {
                _tags.Clear();
            }
            Modified?.Invoke(this, EventArgs.Empty);
        }

        public int Count
        {
            get
            {
                lock (_tags)
                {
                    return _tags.Count;
                }
            }
        }

        public bool Contains(string tag) 
        {
            lock (_tags)
            {
                return _tags.ContainsValue(tag.Trim().ToLower());
            }
        }

        /// <summary>
        /// Returns the tag string for the given tag id
        /// </summary>
        /// <param name="tagId"></param>
        /// <returns></returns>
        public string this[short tagId]
        {
            get
            {
                lock (_tags)
                {
                    return _tags[tagId];
                }
            }
        }

        /// <summary>
        /// Returns the tag id for the given tag string
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        public short this[string tag]
        {
            get
            {
                lock (_tags)
                {
                    return GetId(tag);
                }
            }
        }

        /// <summary>
        /// Convert tags to binary data
        /// </summary>
        /// <returns></returns>
        public byte[] ToBinary()
        {
            lock (_tags)
            {
                var bytes = new List<byte>();
                foreach (var tag in _tags)
                {
                    bytes.AddRange(BitConverter.GetBytes(tag.Key));
                    bytes.AddRange(BitConverter.GetBytes(tag.Value.Length));
                    bytes.AddRange(Encoding.UTF8.GetBytes(tag.Value));
                }
                return bytes.ToArray();
            }
        }

        /// <summary>
        /// Deserialize binary data to tags
        /// </summary>
        /// <param name="data"></param>
        public void FromBinary(byte[] data)
        {
            lock (_tags)
            {
                _tags.Clear();
                for (int i = 0; i < data.Length;)
                {
                    short tagId = BitConverter.ToInt16(data, i);
                    i += sizeof(short);
                    int tagLength = BitConverter.ToInt32(data, i);
                    i += sizeof(int);
                    string tag = Encoding.UTF8.GetString(data, i, tagLength);
                    i += tagLength;
                    _tags.Add(tagId, tag);
                }
            }
        }

        /// <summary>
        /// Get a text representation of the tags
        /// </summary>
        /// <param name="tagIds"></param>
        /// <returns></returns>
        public string GetRangeAsString(short[] tagIds)
        {
            lock (_tags)
            {
                if (tagIds == null || tagIds.Length == 0)
                {
                    return string.Empty;
                }
                var tags = new List<string>();
                foreach (var tagId in tagIds)
                {
                    tags.Add(_tags[tagId]);
                }
                return string.Join(", ", tags);
            }
        }

        /// <summary>
        /// Creates a tag map for the vectors
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public void BuildMap()
        {
            if (_vectorList == null || _vectorList.Count == 0)
            {
                throw new ArgumentNullException(nameof(VectorList));
            }
            lock (_tagMap)
            {
                _tagMap.Clear();
                for (int i = 0; i < _vectorList.Count; i++)
                {
                    var vector = _vectorList[i];
                    foreach (var tagId in vector.Tags)
                    {
                        if (!_tagMap.ContainsKey(tagId))
                        {
                            _tagMap.Add(tagId, new List<Guid>());
                        }
                        _tagMap[tagId].Add(vector.Id);
                    }
                }
            }

        }

        /// <summary>
        /// Return a list of all Tags in their human-readable format
        /// </summary>
        /// <returns></returns>
        public IAsyncEnumerable<string> GetAll()
        {
            lock (_tags)
            {
                return _tags.Values.ToAsyncEnumerable();
            }
        }

        public void Remove(short tagId)
        {
            Modified?.Invoke(this, EventArgs.Empty);
            throw new NotImplementedException();
        }
    }
}
