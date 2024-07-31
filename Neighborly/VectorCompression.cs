using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    public partial class Vector
    {
        public byte[] ToCompressedBinary()
        {
            using (var compressor = new FpZipCompression())
            {
                // Initialize the compressor for writing
                compressor.InitializeForWriting(
                    type: 0, // Assuming type 0 for float
                    prec: 32, // Assuming 32-bit precision for float
                    nx: Values.Length, // Number of elements in the vector
                    ny: 1,
                    nz: 1,
                    nf: 1,
                    bufferSize: Values.Length * sizeof(float)
                );

                // Compress the vector values
                return compressor.Compress(Values);
            }
        }

        public static Vector FromCompressedBinary(byte[] data)
        {
            using (var decompressor = new FpZipCompression())
            {
                // Extract the original length of the Values array from the data
                int originalLength = BitConverter.ToInt32(data, 0);

                // Extract the compressed data
                byte[] compressedData = new byte[data.Length - sizeof(int)];
                Buffer.BlockCopy(data, sizeof(int), compressedData, 0, compressedData.Length);

                // Initialize the decompressor for reading
                decompressor.InitializeForReading(compressedData);

                // Decompress the data
                float[] decompressedValues = decompressor.Decompress(originalLength);

                // Create a new Vector instance with the decompressed values
                return new Vector(decompressedValues);
            }
        }
    }
}
