using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    public class HelperFunctions
    {
        public static byte[] Compress(byte[] data)
        {
            using (var originalStream = new MemoryStream(data))
            {
                using (var compressedStream = new MemoryStream())
                {
                    using (var compressionStream = new GZipStream(compressedStream, CompressionMode.Compress))
                    {
                        originalStream.CopyTo(compressionStream);
                    }
                    return compressedStream.ToArray();
                }
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            {
                using (var decompressedStream = new MemoryStream())
                {
                    using (var decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedStream);
                    }
                    return decompressedStream.ToArray();
                }
            }
        }

        public static byte[] SerializeToBinary<T>(T item)
        {
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                var bytes = Convert.ChangeType(item, typeof(byte[])) as byte[];
                if (bytes != null)
                {
                    writer.Write(bytes);
                }
                return memoryStream.ToArray();
            }
        }

        public static T DeserializeFromBinary<T>(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(memoryStream))
            {
                var itemBytes = reader.ReadBytes(bytes.Length);
                return (T)Convert.ChangeType(itemBytes, typeof(T));
            }
        }

        public static void WriteToFile(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        public static byte[] ReadFromFile(string path)
        {
            return File.ReadAllBytes(path);
        }
    }
}
