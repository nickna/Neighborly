using FpZip;

namespace Neighborly
{
    public partial class Vector
    {
        // Marker byte to identify empty vector compressed format
        private static readonly byte[] EmptyVectorMarker = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Compresses the vector values to a binary format using FpZip lossless compression.
        /// </summary>
        /// <returns>Compressed byte array.</returns>
        public byte[] ToCompressedBinary()
        {
            // FpZip requires nx > 0, handle empty vectors specially
            if (Values.Length == 0)
            {
                return EmptyVectorMarker;
            }

            return FpZipCompressor.Compress(Values, nx: Values.Length);
        }

        /// <summary>
        /// Creates a Vector from compressed binary data.
        /// </summary>
        /// <param name="data">Compressed byte array from ToCompressedBinary.</param>
        /// <returns>Decompressed Vector instance.</returns>
        public static Vector FromCompressedBinary(byte[] data)
        {
            // Check for empty vector marker
            if (data.Length == EmptyVectorMarker.Length &&
                data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x00)
            {
                return new Vector(Array.Empty<float>());
            }

            float[] decompressedValues = FpZipCompressor.DecompressFloat(data);
            return new Vector(decompressedValues);
        }
    }
}
