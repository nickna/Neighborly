﻿using Microsoft.Win32.SafeHandles;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly;

/// <summary>
/// Provides services for working with memory-mapped files, including creating sparse files and retrieving file information.
/// </summary>
internal static class MemoryMappedFileServices
{
    // P/Invoke declaration for CreateFile function from kernel32.dll
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern SafeFileHandle CreateFile(
    string lpFileName,
        [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
        [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    // P/Invoke declaration for DeviceIoControl function from kernel32.dll
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

    // Control code for setting a file as sparse
    const uint FSCTL_SET_SPARSE = 0x900C4;

    // Win32 P/Invoke declarations
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint GetCompressedFileSize(
        string lpFileName,
        out uint lpFileSizeHigh);

    // Linux and FreeBSD P/Invoke declarations
    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int stat(string path, out StatBuffer statbuf);

    // Struct for holding file statistics (Linux and FreeBSD)
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
    /// On Windows, this function sets the sparse file attribute on the file at the given path.
    /// Call this function before opening the file with a MemoryMappedFile.
    /// </summary>
    /// <param name="path">The path of the file to set as sparse.</param>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown when a Win32 error occurs.</exception>
    internal static void WinFileAlloc(string path)
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
            FileShare.ReadWrite,
            IntPtr.Zero,
            FileMode.Create,
            FileAttributes.Normal | (FileAttributes)0x200, // FILE_ATTRIBUTE_SPARSE_FILE
            IntPtr.Zero);

        if (fileHandle.IsInvalid)
        {
            Logging.Logger.Error("Failed while trying to set this file as sparse: {Path}. Error: {Error} ", path, Marshal.GetLastWin32Error());

            // throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
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

    /// <summary>
    /// Returns the actual disk space used by the Index and Data files.
    /// </summary>
    /// <param name="indexFile">The memory-mapped file holder for the index file.</param>
    /// <param name="dataFile">The memory-mapped file holder for the data file.</param>
    /// <returns>
    /// An array containing:
    /// [0] = bytes allocated for Index file,
    /// [1] = total (sparse) capacity of Index file,
    /// [2] = bytes allocated for Data file,
    /// [3] = total (sparse) capacity of Data file.
    /// </returns>
    /// <seealso cref="ForceFlush"/>
    internal static long[] GetFileInfo(MemoryMappedFileHolder indexFile, MemoryMappedFileHolder dataFile)
    {
        // Return the disk info for _indexFile and _dataFile as a long[] array
        long[] fileInfo = new long[4];

        fileInfo[0] = GetActualDiskSpaceUsed(indexFile.Filename);
        fileInfo[1] = indexFile.Capacity;
        fileInfo[2] = GetActualDiskSpaceUsed(dataFile.Filename);
        fileInfo[3] = dataFile.Capacity;
        return fileInfo;
    }

    /// <summary>
    /// Gets the actual disk space used by a file.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>The actual disk space used by the file in bytes.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown when a Win32 error occurs.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when the operating system is not supported.</exception>
    internal static long GetActualDiskSpaceUsed(string fileName)
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
    /// Creates a new database title based on the current date and time.
    /// </summary>
    /// <param name="dbTitle"></param>
    /// <returns>string that is usable in a filename on all supported filesystems</returns>
    internal static string CreateDbTitle(string? dbTitle)
    {
        if (string.IsNullOrEmpty(dbTitle))
        {
            dbTitle = DateTime.Now.ToString("yyyyMMddHHmmss");
        }
        else
        {
            dbTitle = System.Text.RegularExpressions.Regex.Replace(dbTitle, @"[^a-zA-Z0-9]", "");
        }
        return dbTitle;
    }

    private static string _FileExtension = "nbrly";

    internal enum FilePurpose
    {
        Index,
        Data,
        Text
    }
    internal static string CreateFileName(string? basePath, string? title, FilePurpose purpose)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            // Get current working directory
            basePath = Directory.GetCurrentDirectory();
        }

        if (string.IsNullOrEmpty(title))
        {
            title = DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        var filePurpose = purpose switch
        {
            FilePurpose.Index => "index",
            FilePurpose.Data => "data",
            FilePurpose.Text => "text",
            _ => throw new ArgumentException("Invalid file purpose", nameof(purpose)),
        };

        return Path.Combine(basePath, $"{title}_{filePurpose}.{_FileExtension}");
    }

    internal static string CreateBasePath(string? basePath)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            // Get current working directory
            return Directory.GetCurrentDirectory();
        }
        return basePath;
    }

    internal static long CreateCapacity(long capacity)
    {
        // TODO: Compute capacity based on system memory
        return capacity;
    }
}
