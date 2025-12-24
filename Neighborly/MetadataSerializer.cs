using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neighborly;

internal static class MetadataSerializer
{
    private const byte MetadataFormatVersion = 1;

    /// <summary>
    /// Initial buffer size for serialization. Will grow dynamically if needed.
    /// </summary>
    private const int InitialBufferSize = 256;

    public static byte[] Serialize(Dictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return [MetadataFormatVersion, 0, 0, 0, 0]; // Version + count (0)
        }

        // Estimate initial buffer size: version(1) + count(4) + avg entry size
        int estimatedSize = Math.Max(InitialBufferSize, 5 + (metadata.Count * 64));
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(estimatedSize);

        try
        {
            int offset = 0;

            // Write version
            rentedBuffer[offset++] = MetadataFormatVersion;

            // Write count using BinaryPrimitives
            BinaryPrimitives.WriteInt32LittleEndian(rentedBuffer.AsSpan(offset), metadata.Count);
            offset += 4;

            // Write each key-value pair
            foreach (var kvp in metadata)
            {
                // Ensure buffer capacity, resize if needed
                offset = EnsureCapacity(ref rentedBuffer, offset, 1024);

                // Write key length and key bytes
                int keyByteCount = Encoding.UTF8.GetByteCount(kvp.Key);
                BinaryPrimitives.WriteInt32LittleEndian(rentedBuffer.AsSpan(offset), keyByteCount);
                offset += 4;

                Encoding.UTF8.GetBytes(kvp.Key, rentedBuffer.AsSpan(offset, keyByteCount));
                offset += keyByteCount;

                // Write value with type info
                offset = SerializeValue(rentedBuffer, offset, kvp.Value, ref rentedBuffer);
            }

            // Create final result array (exact size)
            byte[] result = new byte[offset];
            rentedBuffer.AsSpan(0, offset).CopyTo(result);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    /// <summary>
    /// Ensures the buffer has enough capacity for additional bytes.
    /// Resizes the buffer if needed.
    /// </summary>
    private static int EnsureCapacity(ref byte[] buffer, int currentOffset, int additionalBytesNeeded)
    {
        if (currentOffset + additionalBytesNeeded <= buffer.Length)
            return currentOffset;

        int newSize = Math.Max(buffer.Length * 2, currentOffset + additionalBytesNeeded);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        buffer.AsSpan(0, currentOffset).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
        return currentOffset;
    }
    
    public static Dictionary<string, object> Deserialize(ReadOnlySpan<byte> data)
    {
        var result = new Dictionary<string, object>();
        
        if (data.Length < 5) // Version + count minimum
            return result;
        
        int offset = 0;
        
        // Read version
        byte version = data[offset];
        offset++;
        
        if (version != MetadataFormatVersion)
            throw new NotSupportedException($"Metadata format version {version} is not supported");
        
        // Read count
        int count = BitConverter.ToInt32(data.Slice(offset, 4));
        offset += 4;
        
        if (count == 0)
            return result;
        
        // Read each key-value pair
        for (int i = 0; i < count; i++)
        {
            // Read key
            int keyLength = BitConverter.ToInt32(data.Slice(offset, 4));
            offset += 4;
            
            string key = Encoding.UTF8.GetString(data.Slice(offset, keyLength));
            offset += keyLength;
            
            // Read value
            (object value, int bytesRead) = DeserializeValue(data.Slice(offset));
            offset += bytesRead;
            
            result[key] = value;
        }
        
        return result;
    }
    
    /// <summary>
    /// Serializes a value to the buffer using BinaryPrimitives for optimal performance.
    /// </summary>
    private static int SerializeValue(byte[] buffer, int offset, object? value, ref byte[] bufferRef)
    {
        switch (value)
        {
            case null:
                buffer[offset++] = (byte)MetadataType.Null;
                break;

            case string s:
                buffer[offset++] = (byte)MetadataType.String;
                int stringByteCount = Encoding.UTF8.GetByteCount(s);
                offset = EnsureCapacity(ref bufferRef, offset, stringByteCount + 4);
                buffer = bufferRef; // Update local reference after potential resize
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), stringByteCount);
                offset += 4;
                Encoding.UTF8.GetBytes(s, buffer.AsSpan(offset, stringByteCount));
                offset += stringByteCount;
                break;

            case int i:
                buffer[offset++] = (byte)MetadataType.Int32;
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), i);
                offset += 4;
                break;

            case long l:
                buffer[offset++] = (byte)MetadataType.Int64;
                BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset), l);
                offset += 8;
                break;

            case float f:
                buffer[offset++] = (byte)MetadataType.Float;
                BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(offset), f);
                offset += 4;
                break;

            case double d:
                buffer[offset++] = (byte)MetadataType.Double;
                BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(offset), d);
                offset += 8;
                break;

            case bool b:
                buffer[offset++] = (byte)MetadataType.Boolean;
                buffer[offset++] = b ? (byte)1 : (byte)0;
                break;

            case DateTime dt:
                buffer[offset++] = (byte)MetadataType.DateTime;
                BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset), dt.ToBinary());
                offset += 8;
                break;

            case string[] sa:
                buffer[offset++] = (byte)MetadataType.StringArray;
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), sa.Length);
                offset += 4;
                foreach (var str in sa)
                {
                    int strByteCount = Encoding.UTF8.GetByteCount(str);
                    offset = EnsureCapacity(ref bufferRef, offset, strByteCount + 4);
                    buffer = bufferRef;
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), strByteCount);
                    offset += 4;
                    Encoding.UTF8.GetBytes(str, buffer.AsSpan(offset, strByteCount));
                    offset += strByteCount;
                }
                break;

            case int[] ia:
                buffer[offset++] = (byte)MetadataType.Int32Array;
                offset = EnsureCapacity(ref bufferRef, offset, (ia.Length * 4) + 4);
                buffer = bufferRef;
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), ia.Length);
                offset += 4;
                foreach (var item in ia)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), item);
                    offset += 4;
                }
                break;

            default:
                // Fall back to JSON for complex types - use source generation when available
                buffer[offset++] = (byte)MetadataType.Json;
                string json = JsonSerializer.Serialize(value, MetadataJsonContext.Default.Object);
                int jsonByteCount = Encoding.UTF8.GetByteCount(json);
                offset = EnsureCapacity(ref bufferRef, offset, jsonByteCount + 4);
                buffer = bufferRef;
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), jsonByteCount);
                offset += 4;
                Encoding.UTF8.GetBytes(json, buffer.AsSpan(offset, jsonByteCount));
                offset += jsonByteCount;
                break;
        }

        return offset;
    }
    
    private static (object value, int bytesRead) DeserializeValue(ReadOnlySpan<byte> data)
    {
        if (data.Length < 1)
            throw new InvalidOperationException("Invalid metadata format");
        
        var type = (MetadataType)data[0];
        int offset = 1;
        
        switch (type)
        {
            case MetadataType.Null:
                return (null!, offset);
                
            case MetadataType.String:
                int stringLength = BitConverter.ToInt32(data.Slice(offset, 4));
                offset += 4;
                string stringValue = Encoding.UTF8.GetString(data.Slice(offset, stringLength));
                offset += stringLength;
                return (stringValue, offset);
                
            case MetadataType.Int32:
                int intValue = BitConverter.ToInt32(data.Slice(offset, 4));
                offset += 4;
                return (intValue, offset);
                
            case MetadataType.Int64:
                long longValue = BitConverter.ToInt64(data.Slice(offset, 8));
                offset += 8;
                return (longValue, offset);
                
            case MetadataType.Float:
                float floatValue = BitConverter.ToSingle(data.Slice(offset, 4));
                offset += 4;
                return (floatValue, offset);
                
            case MetadataType.Double:
                double doubleValue = BitConverter.ToDouble(data.Slice(offset, 8));
                offset += 8;
                return (doubleValue, offset);
                
            case MetadataType.Boolean:
                bool boolValue = BitConverter.ToBoolean(data.Slice(offset, 1));
                offset += 1;
                return (boolValue, offset);
                
            case MetadataType.DateTime:
                long dateTimeBinary = BitConverter.ToInt64(data.Slice(offset, 8));
                offset += 8;
                return (DateTime.FromBinary(dateTimeBinary), offset);
                
            case MetadataType.StringArray:
                int arrayLength = BitConverter.ToInt32(data.Slice(offset, 4));
                offset += 4;
                var stringArray = new string[arrayLength];
                for (int i = 0; i < arrayLength; i++)
                {
                    int itemLength = BitConverter.ToInt32(data.Slice(offset, 4));
                    offset += 4;
                    stringArray[i] = Encoding.UTF8.GetString(data.Slice(offset, itemLength));
                    offset += itemLength;
                }
                return (stringArray, offset);
                
            case MetadataType.Int32Array:
                int intArrayLength = BitConverter.ToInt32(data.Slice(offset, 4));
                offset += 4;
                var intArray = new int[intArrayLength];
                for (int i = 0; i < intArrayLength; i++)
                {
                    intArray[i] = BitConverter.ToInt32(data.Slice(offset, 4));
                    offset += 4;
                }
                return (intArray, offset);
                
            case MetadataType.Json:
                int jsonLength = BitConverter.ToInt32(data.Slice(offset, 4));
                offset += 4;
                string json = Encoding.UTF8.GetString(data.Slice(offset, jsonLength));
                offset += jsonLength;
                var jsonValue = JsonSerializer.Deserialize<object>(json);
                return (jsonValue!, offset);
                
            default:
                throw new NotSupportedException($"Metadata type {type} is not supported");
        }
    }
    
    private enum MetadataType : byte
    {
        Null = 0,
        String = 1,
        Int32 = 2,
        Int64 = 3,
        Float = 4,
        Double = 5,
        Boolean = 6,
        DateTime = 7,
        StringArray = 8,
        Int32Array = 9,
        Json = 255
    }
}

/// <summary>
/// JSON source generation context for metadata serialization.
/// Provides AOT-compatible and faster JSON serialization for complex types.
/// </summary>
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal partial class MetadataJsonContext : JsonSerializerContext
{
}