using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Neighborly;

internal static class MetadataSerializer
{
    private const byte MetadataFormatVersion = 1;
    
    public static byte[] Serialize(Dictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return new byte[] { MetadataFormatVersion, 0, 0, 0, 0 }; // Version + count (0)
        }
        
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        
        // Write version
        writer.Write(MetadataFormatVersion);
        
        // Write count
        writer.Write(metadata.Count);
        
        // Write each key-value pair
        foreach (var kvp in metadata)
        {
            // Write key
            var keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
            writer.Write(keyBytes.Length);
            writer.Write(keyBytes);
            
            // Write value with type info
            SerializeValue(writer, kvp.Value);
        }
        
        return stream.ToArray();
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
    
    private static void SerializeValue(BinaryWriter writer, object value)
    {
        switch (value)
        {
            case null:
                writer.Write((byte)MetadataType.Null);
                break;
            case string s:
                writer.Write((byte)MetadataType.String);
                var stringBytes = Encoding.UTF8.GetBytes(s);
                writer.Write(stringBytes.Length);
                writer.Write(stringBytes);
                break;
            case int i:
                writer.Write((byte)MetadataType.Int32);
                writer.Write(i);
                break;
            case long l:
                writer.Write((byte)MetadataType.Int64);
                writer.Write(l);
                break;
            case float f:
                writer.Write((byte)MetadataType.Float);
                writer.Write(f);
                break;
            case double d:
                writer.Write((byte)MetadataType.Double);
                writer.Write(d);
                break;
            case bool b:
                writer.Write((byte)MetadataType.Boolean);
                writer.Write(b);
                break;
            case DateTime dt:
                writer.Write((byte)MetadataType.DateTime);
                writer.Write(dt.ToBinary());
                break;
            case string[] sa:
                writer.Write((byte)MetadataType.StringArray);
                writer.Write(sa.Length);
                foreach (var str in sa)
                {
                    var bytes = Encoding.UTF8.GetBytes(str);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                break;
            case int[] ia:
                writer.Write((byte)MetadataType.Int32Array);
                writer.Write(ia.Length);
                foreach (var item in ia)
                {
                    writer.Write(item);
                }
                break;
            default:
                // Fall back to JSON for complex types
                writer.Write((byte)MetadataType.Json);
                var json = JsonSerializer.Serialize(value);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                writer.Write(jsonBytes.Length);
                writer.Write(jsonBytes);
                break;
        }
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