using Neighborly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestFixture]
    public class VectorCompressionTests
    {
        private static readonly float[] compressibleFloatArray = [
            0.0f, 0.0f, 0.0f, 0.0f, 0.0f,             // Repeated zeros
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f,             // Small increments
            1.0f, 1.0f, 1.0f, 1.0f, 1.0f,             // Repeated ones
            0.001f, 0.002f, 0.003f, 0.004f, 0.005f,   // Very small values
            10.0f, 10.1f, 10.2f, 10.3f, 10.4f,        // Values close to each other
            100.0f, 100.0f, 100.1f, 100.1f, 100.2f,   // Repeated values with small changes
            -1.0f, -0.5f, 0.0f, 0.5f, 1.0f,           // Symmetrical values
            3.14159f, 3.14159f, 3.14159f, 3.14159f,   // Repeated pi value
            0.1f, 0.01f, 0.001f, 0.0001f, 0.00001f    // Decreasing small values
        ];

        [Test]
        public void CompressAndDecompressFullPrecision()
        {
            byte[] compressedData = Vector.Compress(VectorPrecision.Full, compressibleFloatArray);
            float[] decompressedData = Vector.Decompress(VectorPrecision.Full, compressedData);
            Assert.That(decompressedData, Is.EqualTo(compressibleFloatArray));
        }

        [Test]
        public void CompressAndDecompressHalfPrecision()
        {
            byte[] compressedData = Vector.Compress(VectorPrecision.Half, compressibleFloatArray);
            float[] decompressedData = Vector.Decompress(VectorPrecision.Half, compressedData);
            Assert.That(decompressedData, Is.EqualTo(compressibleFloatArray).Within(0.001f));
        }

        [Test]
        public void CompressAndDecompressQuantized8()
        {
            byte[] compressedData = Vector.Compress(VectorPrecision.Quantized8, compressibleFloatArray);
            float[] decompressedData = Vector.Decompress(VectorPrecision.Quantized8, compressedData);
            Assert.That(decompressedData, Is.EqualTo(compressibleFloatArray).Within(0.01f));
        }

        [Test]
        public void CompressAndDecompressUnknownPrecision()
        {
            Assert.Throws<InvalidOperationException>(() => Vector.Compress((VectorPrecision)3, compressibleFloatArray));
            Assert.Throws<InvalidOperationException>(() => Vector.Decompress((VectorPrecision)3, new byte[0]));
        }

    }
}