using System;
using System.Runtime.InteropServices;

public class FpZipCompression : IDisposable
{
    private IntPtr _fpz;
    private bool _isWriteMode;
    private IntPtr _buffer;
    private int _bufferSize;

    public FpZipCompression()
    {
        _fpz = IntPtr.Zero;
        _buffer = IntPtr.Zero;
    }

    public void InitializeForWriting(int type, int prec, int nx, int ny, int nz, int nf, int bufferSize)
    {
        _bufferSize = bufferSize;
        _buffer = Marshal.AllocHGlobal(_bufferSize);
        IntPtr sizePtr = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(sizePtr, (IntPtr)_bufferSize);

        _fpz = FpZipLib.fpzip_write_to_buffer(_buffer, sizePtr);
        if (_fpz == IntPtr.Zero)
            throw new Exception($"Failed to initialize fpzip for writing. Error: {FpZipLib.fpzip_errno()}");

        FpZipLib.FPZ fpzStruct = new FpZipLib.FPZ
        {
            type = type,
            prec = prec,
            nx = nx,
            ny = ny,
            nz = nz,
            nf = nf
        };
        Marshal.StructureToPtr(fpzStruct, _fpz, false);

        if (FpZipLib.fpzip_write_header(_fpz) == 0)
            throw new Exception($"Failed to write fpzip header. Error: {FpZipLib.fpzip_errno()}");

        _isWriteMode = true;
        Marshal.FreeHGlobal(sizePtr);
    }

    public void InitializeForReading(byte[] compressedData)
    {
        _bufferSize = compressedData.Length;
        _buffer = Marshal.AllocHGlobal(_bufferSize);
        Marshal.Copy(compressedData, 0, _buffer, _bufferSize);

        _fpz = FpZipLib.fpzip_read_from_buffer(_buffer);
        if (_fpz == IntPtr.Zero)
            throw new Exception($"Failed to initialize fpzip for reading. Error: {FpZipLib.fpzip_errno()}");

        if (FpZipLib.fpzip_read_header(_fpz) == 0)
            throw new Exception($"Failed to read fpzip header. Error: {FpZipLib.fpzip_errno()}");

        _isWriteMode = false;
    }

    public byte[] Compress(float[] data)
    {
        if (!_isWriteMode)
            throw new InvalidOperationException("FpZipCompression is not initialized for writing");

        IntPtr dataPtr = Marshal.AllocHGlobal(data.Length * sizeof(float));
        Marshal.Copy(data, 0, dataPtr, data.Length);

        IntPtr compressedSize = FpZipLib.fpzip_write(_fpz, dataPtr);
        if (compressedSize == IntPtr.Zero)
            throw new Exception($"Compression failed. Error: {FpZipLib.fpzip_errno()}");

        int size = (int)compressedSize;
        byte[] compressedData = new byte[size];
        Marshal.Copy(_buffer, compressedData, 0, size);

        Marshal.FreeHGlobal(dataPtr);
        return compressedData;
    }

    public float[] Decompress(int dataLength)
    {
        if (_isWriteMode)
            throw new InvalidOperationException("FpZipCompression is not initialized for reading");

        float[] decompressedData = new float[dataLength];
        IntPtr dataPtr = Marshal.AllocHGlobal(dataLength * sizeof(float));

        IntPtr bytesRead = FpZipLib.fpzip_read(_fpz, dataPtr);
        if (bytesRead == IntPtr.Zero)
            throw new Exception($"Decompression failed. Error: {FpZipLib.fpzip_errno()}");

        Marshal.Copy(dataPtr, decompressedData, 0, dataLength);
        Marshal.FreeHGlobal(dataPtr);

        return decompressedData;
    }

    public void Dispose()
    {
        if (_fpz != IntPtr.Zero)
        {
            if (_isWriteMode)
                FpZipLib.fpzip_write_close(_fpz);
            else
                FpZipLib.fpzip_read_close(_fpz);

            _fpz = IntPtr.Zero;
        }

        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
    }
}