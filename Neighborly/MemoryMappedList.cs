using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Collections;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Neighborly;

public class MemoryMappedList : IDisposable, IEnumerable<Vector>
{
    private const int s_idBytesLength = 16;
    private const int s_offsetBytesLength = sizeof(long);
    private const int s_lengthBytesLength = sizeof(int);
    private const int s_indexEntryByteLength = s_idBytesLength + s_offsetBytesLength + s_lengthBytesLength;
    private static readonly Guid s_tombStone;
    private static readonly byte[] s_tombStoneBytes;
    private readonly MemoryMappedFileHolder _indexFile;
    private readonly MemoryMappedFileHolder _dataFile;
    private readonly object _mutex = new();
    private long _count;
    /// <summary>
    /// Indicates if the index stream is at the end of the stream.
    /// This is used to enable fast adding of multiple vectors in sequence.
    /// </summary>
    private bool _isAtEndOfIndexStream = true;
    private bool _disposedValue;
    private long _defragPosition = 0; // Tracks the current position in the index file for defragmentation
    private long _defragIndexPosition;
    private long _newDataPosition;
    private const int _defragBatchSize = 100; // Number of entries to defrag in one batch, adjust based on performance needs

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern SafeFileHandle CreateFile(
      string lpFileName,
      [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
      [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
      IntPtr lpSecurityAttributes,
      [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
      [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
      IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    const uint FSCTL_SET_SPARSE = 0x900C4;

    // Win32 P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetCompressedFileSize(
        string lpFileName,
        out uint lpFileSizeHigh);

    // Linux and FreeBSD P/Invoke declarations
    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int stat(string path, out StatBuffer statbuf);

    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuffer
    {
        public ulong st_dev;
        public ulong st_ino;
        public ulong st_nlink;
        public uint st_mode;
        public uint st_uid;
        public uint st_gid;
        public ulong st_rdev;
        public long st_size;
        public long st_blksize;
        public long st_blocks;
        public long st_atime;
        public long st_mtime;
        public long st_ctime;
        public long st_atime_nsec;
        public long st_mtime_nsec;
        public long st_ctime_nsec;
    }

    /// <summary>
    /// Returns the actual disk space used by the Index and Data files.
    /// </summary>
    /// <returns>
    /// [0] = bytes allocated for Index file
    /// [1] = total (sparce) capacity of Index file
    /// [2] = bytes allocated for Data file
    /// [3] = total (sparce) capacity of Data file
    /// </returns>
    /// <seealso cref="ForceFlush"/>
    public long[] GetFileInfo()
    {
        // Return the disk info for _indexFile and _dataFile as a long[] array
        long[] fileInfo = new long[4];

        fileInfo[0] = _GetActualDiskSpaceUsed(_indexFile.Filename);
        fileInfo[1] = _indexFile.Capacity;
        fileInfo[2] = _GetActualDiskSpaceUsed(_dataFile.Filename);
        fileInfo[3] = _dataFile.Capacity;
        return fileInfo;
    }
    private long _GetActualDiskSpaceUsed(string fileName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            uint fileSizeHigh;
            uint fileSizeLow = GetCompressedFileSize(fileName, out fileSizeHigh);

            if (fileSizeLow == 0xFFFFFFFF && Marshal.GetLastWin32Error() != 0)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            return ((long)fileSizeHigh << 32) + fileSizeLow;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            StatBuffer statbuf;
            if (stat(fileName, out statbuf) != 0)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            return statbuf.st_blocks * 512; // st_blocks is the number of 512-byte blocks allocated
        }
        else
        {
            throw new PlatformNotSupportedException("The operating system is not supported.");
        }
    }
    /// <summary>
    /// Forces the data in the memory-mapped files to be flushed to disk.
    /// </summary>
    public void ForceFlush()
    {
        _indexFile.Stream.Flush();
        _dataFile.Stream.Flush();
    }

    static MemoryMappedList()
    {
        s_tombStone = Guid.NewGuid();

        Span<byte> tombStoneBytes = stackalloc byte[16];
        if (!s_tombStone.TryWriteBytes(tombStoneBytes))
        {
            throw new InvalidOperationException("Failed to write the tombstone to bytes");
        }

        s_tombStoneBytes = tombStoneBytes.ToArray();
    }

    public MemoryMappedList(long capacity)
    {
        _indexFile = new(s_indexEntryByteLength * capacity);
        // Based on typical vector dimensions, 4096 bytes should be enough for most cases as of 2024-06
        _dataFile = new(4096L * capacity);
    }

    /// <summary>
    /// On Windows this function sets the sparse file attribute on the file at the given path.
    /// Call this function before opening the file with a MemoryMappedFile.
    /// </summary>
    /// <param name="path"></param>
    /// <exception cref="System.ComponentModel.Win32Exception"></exception>
    private static void _WinFileAlloc(string path)
    {
        // Only run this function on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
        {
            return;
        }

        // Create a sparse file
        SafeFileHandle fileHandle = CreateFile(
            path,
            FileAccess.ReadWrite,
            FileShare.None,
            IntPtr.Zero,
            FileMode.Create,
            FileAttributes.Normal | (FileAttributes)0x200, // FILE_ATTRIBUTE_SPARSE_FILE
            IntPtr.Zero);

        if (fileHandle.IsInvalid)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        uint bytesReturned;
        bool result = DeviceIoControl(
            fileHandle,
            FSCTL_SET_SPARSE,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            out bytesReturned,
            IntPtr.Zero);

        if (!result)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        // Close the file handle
        fileHandle.Close();
    }
    public long Count
    {
        get
        {
            lock (_mutex)
            {
                return _count;
            }
        }
    }

#pragma warning disable CA1822 // Mark members as static - mimmicking ICollection<Vector>
    public bool IsReadOnly => false;
#pragma warning restore CA1822 // Mark members as static - mimmicking ICollection<Vector>

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _dataFile.Dispose();
                _indexFile.Dispose();
            }

            _disposedValue = true;
        }
    }

    ~MemoryMappedList()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Vector? GetVector(long index)
    {
        lock (_mutex)
        {
            if (index < 0L || index >= _count)
            {
                return null;
            }

            _indexFile.Stream.Seek(index * s_indexEntryByteLength, SeekOrigin.Begin);
            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            _indexFile.Stream.ReadExactly(entry);

            Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
            Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
            long offset = BitConverter.ToInt64(offsetBytes);
            int length = BitConverter.ToInt32(lengthBytes);
            if (offset < 0L || length <= 0)
            {
                return null;
            }

            _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
            Span<byte> bytes = stackalloc byte[length];
            _dataFile.Stream.ReadExactly(bytes);
            return new Vector(bytes);
        }
    }

    public Vector? GetVector(Guid id)
    {
        lock (_mutex)
        {
            (_, long offset, int length) = SearchVectorInIndex(id);
            if (offset < 0L || length <= 0)
            {
                return null;
            }

            _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
            Span<byte> bytes = stackalloc byte[length];
            _dataFile.Stream.ReadExactly(bytes);
            return new Vector(bytes);
        }
    }

    public void CopyTo(Vector[] array, int arrayIndex)
    {
        lock (_mutex)
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

            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }
    }

    public long FindIndexById(Guid id)
    {
        lock (_mutex)
        {
            (long index, _, _) = SearchVectorInIndex(id);
            return index;
        }
    }

    public long IndexOf(Vector item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_mutex)
        {
            (long index, _, _) = SearchVectorInIndex(item.Id);
            return index;
        }
    }

    public void Add(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        lock (_mutex)
        {
            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            if (!_isAtEndOfIndexStream)
            {
                ReadToEnd();
            }

            Span<byte> idBytes = entry[..s_idBytesLength];
            if (!vector.Id.TryWriteBytes(idBytes))
            {
                throw new InvalidOperationException("Failed to write the Id to bytes");
            }

            Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
            if (!BitConverter.TryWriteBytes(offsetBytes, _dataFile.Stream.Position))
            {
                throw new InvalidOperationException("Failed to write the offset to bytes");
            }

            byte[] data = vector.ToBinary();
            Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
            if (!BitConverter.TryWriteBytes(lengthBytes, data.Length))
            {
                throw new InvalidOperationException("Failed to write the length to bytes");
            }

            _indexFile.Stream.Write(entry);
            _dataFile.Stream.Write(data);

            ++_count;
            _isAtEndOfIndexStream = true;
        }
    }

    public bool Remove(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        lock (_mutex)
        {
            _isAtEndOfIndexStream = false;
            _indexFile.Stream.Seek(0, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;
            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                Guid id = new(entry[..s_idBytesLength]);
                if (id == vector.Id)
                {
                    // Seek to the beginning of the entry and remove the ID with a tombstone-ID
                    ReverseIndexStreamByIdBytesLength();
                    _indexFile.Stream.Write(s_tombStoneBytes);
                    // Seek to after the entry
                    _indexFile.Stream.Seek(s_indexEntryByteLength - s_idBytesLength, SeekOrigin.Current);
                    --_count;
                    return true;
                }
                else if (id.Equals(Guid.Empty))
                {
                    _isAtEndOfIndexStream = true;
                    ReverseIndexStreamByIdBytesLength();
                    break;
                }
            }
        }

        return false;
    }

    public bool Update(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        lock (_mutex)
        {
            if (Remove(vector))
            {
                Add(vector);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Calculate the fragmentation of the data file
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public long CalculateFragmentation()
    {
        lock (_mutex)
        {
            long expectedDataPosition = 0; // Expected start position of the next data entry
            long totalFragmentation = 0; // Total size of gaps between data entries
            long totalDataSize = 0; // Total size of data entries

            _indexFile.Stream.Seek(0, SeekOrigin.Begin); // Start from the beginning of the index file

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength]; // Buffer for reading index entries
            int bytesRead;

            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0) // Read each index entry
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    // If the read entry is incomplete, skip it and continue to the next entry
                    _indexFile.Stream.Seek(s_indexEntryByteLength - bytesRead, SeekOrigin.Current);
                    continue;
                }

                Guid id = new(entry[..s_idBytesLength]); // Extract the ID from the entry
                if (id.Equals(s_tombStone) || id.Equals(Guid.Empty))
                {
                    continue; // Skip tombstoned or empty entries
                }

                long actualOffset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength)); // Actual start position of the data entry
                int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength)); // Length of the data entry

                if (actualOffset > expectedDataPosition)
                {
                    // If there's a gap between the expected and actual position, it's fragmentation
                    long gapSize = actualOffset - expectedDataPosition;
                    totalFragmentation += gapSize;
                }

                // Update the expected position for the next entry
                expectedDataPosition = actualOffset + length;
                totalDataSize += length;
            }

            if (totalDataSize == 0)
            {
                return 0;
            }

            return totalFragmentation * 100 / totalDataSize;
        }
    }



    /// <summary>
    /// Performs a blocking defragmentation of the data file, regardless of the fragmentation level
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Defrag()
    {
        lock (_mutex)
        {
            long newIndexPosition = 0;
            long newDataPosition = 0;

            _indexFile.Stream.Seek(0, SeekOrigin.Begin);
            _dataFile.Stream.Seek(0, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;

            while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                Guid id = new(entry[..s_idBytesLength]);
                if (id.Equals(s_tombStone))
                {
                    continue; // Skip tombstoned entries
                }

                if (id.Equals(Guid.Empty))
                {
                    break; // End of valid entries
                }

                long offset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength));
                int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));

                // Read data associated with the entry
                byte[] data = new byte[length];
                _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
                _dataFile.Stream.ReadExactly(data);

                // Update the offset in the index entry to the new data position
                BitConverter.TryWriteBytes(entry.Slice(s_idBytesLength, s_offsetBytesLength), newDataPosition);

                // Write the updated index entry back to the index file at the new position
                _indexFile.Stream.Seek(newIndexPosition * s_indexEntryByteLength, SeekOrigin.Begin);
                _indexFile.Stream.Write(entry);

                // Write the data back to the data file at the new position
                _dataFile.Stream.Seek(newDataPosition, SeekOrigin.Begin);
                _dataFile.Stream.Write(data);

                newIndexPosition++;
                newDataPosition += length;
            }
        }
    }

    /// <summary>
    /// Defragments the data file in batches, to avoid blocking I/O for long periods
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public long DefragBatch()
    {
        lock (_mutex)
        {
            long newIndexPosition = _defragIndexPosition;
            long newDataPosition = _newDataPosition;
            long totalDataSize = 0;
            long totalFragmentation = 0;
            int entriesProcessed = 0;

            _indexFile.Stream.Seek(newIndexPosition * s_indexEntryByteLength, SeekOrigin.Begin);
            _dataFile.Stream.Seek(newDataPosition, SeekOrigin.Begin);

            Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
            int bytesRead;
            List<(Guid id, long oldOffset, int length, long newOffset)> updates = new List<(Guid, long, int, long)>(_defragBatchSize);

            // Find the maximum entry size in this batch
            int maxEntrySize = 0;

            while (entriesProcessed < _defragBatchSize && newIndexPosition < _count)
            {
                bytesRead = _indexFile.Stream.Read(entry);
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                Guid id = new(entry[..s_idBytesLength]);
                if (id.Equals(s_tombStone))
                {
                    newIndexPosition++;
                    continue;
                }

                if (id.Equals(Guid.Empty))
                {
                    break;
                }

                long offset = BitConverter.ToInt64(entry.Slice(s_idBytesLength, s_offsetBytesLength));
                int length = BitConverter.ToInt32(entry.Slice(s_idBytesLength + s_offsetBytesLength, s_lengthBytesLength));

                if (offset > newDataPosition)
                {
                    totalFragmentation += offset - newDataPosition;
                }

                updates.Add((id, offset, length, newDataPosition));
                maxEntrySize = Math.Max(maxEntrySize, length);

                newIndexPosition++;
                newDataPosition += length;
                totalDataSize += length;
                entriesProcessed++;
            }

            // Rent a buffer from the ArrayPool
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(maxEntrySize);

            try
            {
                // Perform all reads and writes
                foreach (var update in updates)
                {
                    // Read data from old position
                    _dataFile.Stream.Seek(update.oldOffset, SeekOrigin.Begin);
                    _dataFile.Stream.Read(sharedBuffer, 0, update.length);

                    // Write data to new position
                    _dataFile.Stream.Seek(update.newOffset, SeekOrigin.Begin);
                    _dataFile.Stream.Write(sharedBuffer, 0, update.length);

                    // Update index entry
                    _indexFile.Stream.Seek(_defragIndexPosition * s_indexEntryByteLength, SeekOrigin.Begin);
                    _indexFile.Stream.Write(update.id.ToByteArray());
                    _indexFile.Stream.Write(BitConverter.GetBytes(update.newOffset));
                    _indexFile.Stream.Write(BitConverter.GetBytes(update.length));

                    _defragIndexPosition++;
                }
            }
            finally
            {
                // Return the buffer to the pool
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }

            // Update tracking variables for the next batch
            _newDataPosition = newDataPosition;

            // Detecting the end of defragmentation
            if (newIndexPosition >= _count)
            {
                // Reset state variables for the next defragmentation cycle
                _defragIndexPosition = 0;
                _newDataPosition = 0;
                return 0; // Defragmentation complete
            }

            // Calculate and return fragmentation percentage
            return totalDataSize == 0 ? 0 : totalFragmentation * 100 / totalDataSize;
        }
    }


    public void Clear()
    {
        lock (_mutex)
        {
            _indexFile.DisposeStreams();
            _indexFile.Reset();
            _dataFile.DisposeStreams();
            _dataFile.Reset();
            _count = 0;
        }
    }

    public bool Contains(Vector item)
    {
        if (item is null)
        {
            return false;
        }

        var vector = GetVector(item.Id);
        if (vector is null)
        {
            return false;
        }

        return vector.Equals(item);
    }

    public IEnumerator<Vector> GetEnumerator()
    {
        lock (_mutex)
        {
            _isAtEndOfIndexStream = false;
            _indexFile.Stream.Seek(0, SeekOrigin.Begin);
            _dataFile.Stream.Seek(0, SeekOrigin.Begin);

            byte[] entry = new byte[s_indexEntryByteLength];
            int bytesRead;
            while ((bytesRead = _indexFile.Stream.Read(entry, 0, entry.Length)) > 0)
            {
                if (bytesRead != s_indexEntryByteLength)
                {
                    throw new InvalidOperationException("Failed to read the index entry");
                }

                var entrySpan = entry.AsSpan();
                Guid id = new(entrySpan[..s_idBytesLength]);
                if (id.Equals(s_tombStone))
                {
                    continue;
                }

                if (id.Equals(Guid.Empty))
                {
                    _isAtEndOfIndexStream = true;
                    ReverseIndexStreamByIdBytesLength();
                    break;
                }

                Span<byte> offsetBytes = entrySpan[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                Span<byte> lengthBytes = entrySpan[(s_idBytesLength + s_offsetBytesLength)..];
                long offset = BitConverter.ToInt64(offsetBytes);
                int length = BitConverter.ToInt32(lengthBytes);

                _dataFile.Stream.Seek(offset, SeekOrigin.Begin);
                Span<byte> bytes = stackalloc byte[length];
                _dataFile.Stream.ReadExactly(bytes);
                yield return new Vector(bytes);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private (long index, long offset, int length) SearchVectorInIndex(Guid id)
    {
        _isAtEndOfIndexStream = false;
        _indexFile.Stream.Seek(0, SeekOrigin.Begin);

        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        int bytesRead;
        long index = 0L;
        while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
        {
            if (bytesRead != s_indexEntryByteLength)
            {
                throw new InvalidOperationException("Failed to read the index entry");
            }

            Guid vectorId = new(entry[..s_idBytesLength]);
            if (id == vectorId)
            {
                Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
                long offset = BitConverter.ToInt64(offsetBytes);
                int length = BitConverter.ToInt32(lengthBytes);

                return (index, offset, length);
            }
            else if (vectorId.Equals(Guid.Empty))
            {
                _isAtEndOfIndexStream = true;
                ReverseIndexStreamByIdBytesLength();
                break;
            }

            ++index;
        }

        return (-1L, -1L, -1);
    }

    /// <summary>
    /// Reads the index file and the data file to the last entry,
    /// so that new data can be appended safely.
    /// </summary>
    private void ReadToEnd()
    {
        _indexFile.Stream.Seek(0, SeekOrigin.Begin);

        // To keep track of the current position in the data file
        // that we can use to append new data.
        // Theoretically, this should always be the last records offset + length,
        // but we can't be sure that the index file is always in sync with the data file.
        long maxOffset = -1L;
        int lengthAtMaxOffset = -1;

        Span<byte> entry = stackalloc byte[s_indexEntryByteLength];
        int bytesRead;
        while ((bytesRead = _indexFile.Stream.Read(entry)) > 0)
        {
            if (bytesRead != s_indexEntryByteLength)
            {
                throw new InvalidOperationException("Failed to read the index entry");
            }

            Guid vectorId = new(entry[..s_idBytesLength]);
            if (!vectorId.Equals(Guid.Empty))
            {
                Span<byte> offsetBytes = entry[s_idBytesLength..(s_idBytesLength + s_offsetBytesLength)];
                Span<byte> lengthBytes = entry[(s_idBytesLength + s_offsetBytesLength)..];
                long offset = BitConverter.ToInt64(offsetBytes);
                int length = BitConverter.ToInt32(lengthBytes);

                if (offset > maxOffset || (offset == maxOffset && length > lengthAtMaxOffset))
                {
                    maxOffset = offset;
                    lengthAtMaxOffset = length;
                }
            }
            else
            {
                _isAtEndOfIndexStream = true;
                ReverseIndexStreamByIdBytesLength();
                _dataFile.Stream.Seek(maxOffset + lengthAtMaxOffset, SeekOrigin.Begin);
                break;
            }
        }
    }

    /// <summary>
    /// If the index stream is not at the beginning, it will be moved to the previous entry.
    /// This is to go back one entry after searching for an entry and getting an empty entry.
    /// </summary>
    private void ReverseIndexStreamByIdBytesLength()
    {
        if (_indexFile.Stream.Position != 0L)
        {
            _indexFile.Stream.Seek(-s_indexEntryByteLength, SeekOrigin.Current);
        }
    }

    private class MemoryMappedFileHolder : IDisposable
    {
        private readonly long _capacity;
        private MemoryMappedFile _file;
        private MemoryMappedViewStream _stream;
        private bool _disposedValue;
        private string _fileName;
        public string Filename
        {
            get { return _fileName; }
        }
        public long Capacity
        {
            get { return _capacity; }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Reset()
        public MemoryMappedFileHolder(long capacity)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable. - Done by a call to Reset()
        {
            _capacity = capacity;

            Reset();
        }

        public MemoryMappedViewStream Stream => _stream;

        public void Reset()
        {
            _fileName = Path.GetTempFileName();
            _WinFileAlloc(_fileName);
            double capacityTiB = _capacity / (1024.0 * 1024.0 * 1024.0 * 1024.0);
            Logging.Logger.Information("Creating temporary file: {FileName}, size {capacity} TiB", _fileName, capacityTiB);
            try
            {
                _file = MemoryMappedFile.CreateFromFile(_fileName, FileMode.OpenOrCreate, null, _capacity);
                _stream = _file.CreateViewStream();
            }
            catch (System.IO.IOException ex)
            {
                if (File.Exists(_fileName))
                {
                    File.Delete(_fileName);
                    Logging.Logger.Error($"Error occurred while trying to create file ({_fileName}). File was successfully deleted. Error: {ex.Message}");
                }
                else
                {
                    Logging.Logger.Error($"Error occurred while trying to create file ({_fileName}). Error: {ex.Message}");
                }
                    throw;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DisposeStreams();
                    try
                    {
                        if (File.Exists(_fileName))
                        {
                            File.Delete(_fileName);
                            Logging.Logger.Information("Deleted temporary file: {FileName}", _fileName);
                        }
                        else
                        {
                            Logging.Logger.Warning("Temporary file not found: {FileName}", _fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Error(ex, "Failed to delete temporary file: {FileName}", _fileName);
                    }
                }

                _disposedValue = true;
            }
        }

        ~MemoryMappedFileHolder()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void DisposeStreams()
        {
            _stream?.Dispose();
            _file?.Dispose();
        }
    }
}