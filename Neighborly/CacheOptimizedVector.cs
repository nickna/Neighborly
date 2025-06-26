using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Neighborly;

/// <summary>
/// Cache-optimized vector implementation with aligned memory layout for improved performance.
/// </summary>
public sealed class CacheOptimizedVector : IDisposable
{
    // Cache line size (64 bytes on x86/x64, configurable for other architectures)
    private const int CacheLineSize = 64;
    
    // Alignment for SIMD operations (32 bytes for AVX, 64 bytes for AVX-512)
    private const int SimdAlignment = 32;
    
    // Maximum alignment we use
    private const int Alignment = CacheLineSize;
    
    // Native memory pointer for aligned data
    private unsafe float* _alignedValues;
    private readonly int _dimension;
    private readonly int _paddedDimension;
    private readonly bool _ownsMemory;
    private bool _disposed;
    
    // Metadata stored separately for better cache efficiency
    public Guid Id { get; private set; }
    public string OriginalText { get; }
    public short[] Tags { get; }
    public VectorAttributes Attributes { get; set; }
    
    /// <summary>
    /// Gets the dimension of the vector.
    /// </summary>
    public int Dimension => _dimension;
    
    /// <summary>
    /// Creates a cache-optimized vector with aligned memory.
    /// </summary>
    public unsafe CacheOptimizedVector(float[] values, string? originalText = null, short[]? tags = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        
        _dimension = values.Length;
        _ownsMemory = true; // This instance owns the memory
        
        // Calculate padded dimension to align to cache line boundaries
        // This ensures the next vector starts on a cache line boundary
        int floatsPerCacheLine = CacheLineSize / sizeof(float);
        _paddedDimension = ((_dimension + floatsPerCacheLine - 1) / floatsPerCacheLine) * floatsPerCacheLine;
        
        // Allocate aligned memory
        AllocateAlignedMemory();
        
        // Copy values to aligned memory
        CopyToAlignedMemory(values);
        
        // Initialize metadata
        Id = Guid.NewGuid();
        OriginalText = originalText ?? string.Empty;
        Tags = tags ?? Array.Empty<short>();
        Attributes = new VectorAttributes();
    }
    
    /// <summary>
    /// Creates a vector from an existing aligned memory pointer (for batch operations).
    /// </summary>
    internal unsafe CacheOptimizedVector(float* alignedValues, int dimension, Guid id, 
                                       string originalText, short[] tags)
    {
        _alignedValues = alignedValues;
        _dimension = dimension;
        _ownsMemory = false; // This instance doesn't own the memory (owned by batch)
        
        int floatsPerCacheLine = CacheLineSize / sizeof(float);
        _paddedDimension = ((dimension + floatsPerCacheLine - 1) / floatsPerCacheLine) * floatsPerCacheLine;
        
        Id = id;
        OriginalText = originalText;
        Tags = tags;
        Attributes = new VectorAttributes();
    }
    
    private unsafe void AllocateAlignedMemory()
    {
        // Allocate memory with alignment
        int totalBytes = _paddedDimension * sizeof(float);
        _alignedValues = (float*)NativeMemory.AlignedAlloc((nuint)totalBytes, Alignment);
        
        if (_alignedValues == null)
        {
            throw new OutOfMemoryException("Failed to allocate aligned memory for vector");
        }
        
        // Zero-initialize the padded area
        NativeMemory.Clear(_alignedValues, (nuint)totalBytes);
    }
    
    private unsafe void CopyToAlignedMemory(float[] values)
    {
        fixed (float* srcPtr = values)
        {
            Buffer.MemoryCopy(srcPtr, _alignedValues, 
                             _paddedDimension * sizeof(float), 
                             values.Length * sizeof(float));
        }
    }
    
    /// <summary>
    /// Gets a span over the vector values for efficient access.
    /// </summary>
    public unsafe ReadOnlySpan<float> GetValues()
    {
        return new ReadOnlySpan<float>(_alignedValues, _dimension);
    }
    
    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    public unsafe float this[int index]
    {
        get
        {
            if (index < 0 || index >= _dimension)
                throw new IndexOutOfRangeException();
                
            return _alignedValues[index];
        }
    }
    
    /// <summary>
    /// Copies values to a regular float array.
    /// </summary>
    public unsafe float[] ToArray()
    {
        var result = new float[_dimension];
        fixed (float* destPtr = result)
        {
            Buffer.MemoryCopy(_alignedValues, destPtr, 
                             result.Length * sizeof(float), 
                             _dimension * sizeof(float));
        }
        return result;
    }
    
    /// <summary>
    /// Provides direct pointer access for SIMD operations.
    /// </summary>
    internal unsafe float* GetAlignedPointer() => _alignedValues;
    
    /// <summary>
    /// Converts to a regular Vector for API compatibility.
    /// </summary>
    public Vector ToVector()
    {
        return new Vector(Id, ToArray(), Tags, OriginalText)
        {
            Attributes = Attributes
        };
    }
    
    /// <summary>
    /// Creates a cache-optimized vector from a regular vector.
    /// </summary>
    public static CacheOptimizedVector FromVector(Vector vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        
        var optimized = new CacheOptimizedVector(vector.Values, vector.OriginalText, vector.Tags);
        optimized.Attributes = vector.Attributes;
        optimized.Id = vector.Id;
        
        return optimized;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            unsafe
            {
                if (_alignedValues != null && _ownsMemory)
                {
                    NativeMemory.AlignedFree(_alignedValues);
                    _alignedValues = null;
                }
            }
            _disposed = true;
        }
    }
    
    ~CacheOptimizedVector()
    {
        Dispose();
    }
}

/// <summary>
/// Batch vector storage for improved cache efficiency in bulk operations.
/// Implements Structure of Arrays (SoA) layout.
/// </summary>
public sealed class CacheOptimizedVectorBatch : IDisposable
{
    private const int CacheLineSize = 64;
    private const int Alignment = CacheLineSize;
    
    private unsafe float* _alignedData;
    private readonly int _vectorCount;
    private readonly int _dimension;
    private readonly int _paddedDimension;
    private readonly Guid[] _ids;
    private readonly string[] _texts;
    private readonly short[][] _tags;
    private bool _disposed;
    
    /// <summary>
    /// Creates a batch of vectors with optimized memory layout.
    /// </summary>
    public unsafe CacheOptimizedVectorBatch(IList<Vector> vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        if (vectors.Count == 0)
            throw new ArgumentException("Vector batch cannot be empty", nameof(vectors));
            
        _vectorCount = vectors.Count;
        _dimension = vectors[0].Dimension;
        
        // Ensure all vectors have same dimension
        foreach (var v in vectors)
        {
            if (v.Dimension != _dimension)
                throw new ArgumentException("All vectors must have the same dimension");
        }
        
        // Calculate padded dimension
        int floatsPerCacheLine = CacheLineSize / sizeof(float);
        _paddedDimension = ((_dimension + floatsPerCacheLine - 1) / floatsPerCacheLine) * floatsPerCacheLine;
        
        // Allocate aligned memory for all vectors
        int totalFloats = _vectorCount * _paddedDimension;
        int totalBytes = totalFloats * sizeof(float);
        _alignedData = (float*)NativeMemory.AlignedAlloc((nuint)totalBytes, Alignment);
        
        if (_alignedData == null)
        {
            throw new OutOfMemoryException("Failed to allocate aligned memory for vector batch");
        }
        
        // Zero-initialize
        NativeMemory.Clear(_alignedData, (nuint)totalBytes);
        
        // Copy data and metadata
        _ids = new Guid[_vectorCount];
        _texts = new string[_vectorCount];
        _tags = new short[_vectorCount][];
        
        for (int i = 0; i < _vectorCount; i++)
        {
            var vector = vectors[i];
            _ids[i] = vector.Id;
            _texts[i] = vector.OriginalText;
            _tags[i] = vector.Tags;
            
            // Copy vector data to aligned memory
            float* destPtr = _alignedData + (i * _paddedDimension);
            fixed (float* srcPtr = vector.Values)
            {
                Buffer.MemoryCopy(srcPtr, destPtr, 
                                 _paddedDimension * sizeof(float), 
                                 vector.Values.Length * sizeof(float));
            }
        }
    }
    
    /// <summary>
    /// Gets a cache-optimized vector at the specified index.
    /// </summary>
    public unsafe CacheOptimizedVector GetVector(int index)
    {
        if (index < 0 || index >= _vectorCount)
            throw new IndexOutOfRangeException();
            
        float* vectorPtr = _alignedData + (index * _paddedDimension);
        return new CacheOptimizedVector(vectorPtr, _dimension, _ids[index], 
                                       _texts[index], _tags[index]);
    }
    
    /// <summary>
    /// Gets a span over a specific vector's values.
    /// </summary>
    public unsafe ReadOnlySpan<float> GetVectorSpan(int index)
    {
        if (index < 0 || index >= _vectorCount)
            throw new IndexOutOfRangeException();
            
        float* vectorPtr = _alignedData + (index * _paddedDimension);
        return new ReadOnlySpan<float>(vectorPtr, _dimension);
    }
    
    /// <summary>
    /// Provides direct access to the aligned data for SIMD operations.
    /// </summary>
    internal unsafe float* GetAlignedDataPointer() => _alignedData;
    
    public int Count => _vectorCount;
    public int Dimension => _dimension;
    
    public void Dispose()
    {
        if (!_disposed)
        {
            unsafe
            {
                if (_alignedData != null)
                {
                    NativeMemory.AlignedFree(_alignedData);
                    _alignedData = null;
                }
            }
            _disposed = true;
        }
    }
    
    ~CacheOptimizedVectorBatch()
    {
        Dispose();
    }
}