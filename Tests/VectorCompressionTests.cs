using Neighborly;
using Neighborly.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly.Tests.Compression
{
    [TestFixture]
    public class VectorCompressionTests
    {
        private VectorDatabase _db;

        [SetUp]
        public void Setup()
        {
            _db?.Dispose();

            _db = new VectorDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            _db.Dispose();
        }

        [Test]
        public void TestCompress4k()
        {
            var vectors = new List<Vector>();
            int[,] vectorSize = new int[TestConstants.Dimensions.Standard, 3];
            float[] compressionRatio = new float[TestConstants.Dimensions.Standard];

            // Create random Vectors of 4k size
            for (int i = 0; i < TestConstants.Dimensions.Standard; i++)
            {
                float[] floats = new float[TestConstants.Dimensions.Compression4K];
                for (int j = 0; j < TestConstants.Dimensions.Compression4K; j++)
                {
                    // Random float
                    floats[j] = (float)Random.Shared.NextDouble();
                }
                vectors.Add(new Vector(floats));

                // Gets size of vectors[i].Values in bytes
                vectorSize[i, 0] = vectors[i].Values.Length * sizeof(float);

                // Compress vectors[i].Values and report the compressed byte length
                vectorSize[i, 1] = vectors[i].ToCompressedBinary().Length;

                // Calculate the compression ratio
                compressionRatio[i] = (float)vectorSize[i, 0] / vectorSize[i, 1];
            }

            // Calculate the median compression ratio
            Array.Sort(compressionRatio);
            float median;
            if (compressionRatio.Length % 2 == 0)
            {
                // Even number of elements
                median = (compressionRatio[compressionRatio.Length / 2 - 1] + compressionRatio[compressionRatio.Length / 2]) / 2;
            }
            else
            {
                // Odd number of elements
                median = compressionRatio[compressionRatio.Length / 2];
            }

            Console.WriteLine($"Median compression ratio: {median:F2}");

            // Compare the sizes of the vectors
            // The compressed versions should be at least 20% less than the original
            Assert.That(median > TestConstants.Compression.MinRatio4K, "Compression ratio is not at least 20% less than the original");
        }


        [Test]
        public void TestCompress512()
        {
            var vectors = new List<Vector>();
            int[,] vectorSize = new int[TestConstants.Dimensions.Standard, 3];
            float[] compressionRatio = new float[TestConstants.Dimensions.Standard];

            // Create random Vectors of 512 size
            for (int i = 0; i < TestConstants.Dimensions.Standard; i++)
            {
                float[] floats = new float[TestConstants.Dimensions.XLarge];
                for (int j = 0; j < TestConstants.Dimensions.XLarge; j++)
                {
                    // Random float
                    floats[j] = (float)Random.Shared.NextDouble();
                }
                vectors.Add(new Vector(floats));

                // Gets size of vectors[i].Values in bytes
                vectorSize[i, 0] = vectors[i].Values.Length * sizeof(float);

                // Compress vectors[i].Values and report the compressed byte length
                vectorSize[i, 1] = vectors[i].ToCompressedBinary().Length;

                // Calculate the compression ratio
                compressionRatio[i] = (float)vectorSize[i, 0] / vectorSize[i, 1];
            }

            // Calculate the median compression ratio
            Array.Sort(compressionRatio);
            float median;
            if (compressionRatio.Length % 2 == 0)
            {
                // Even number of elements
                median = (compressionRatio[compressionRatio.Length / 2 - 1] + compressionRatio[compressionRatio.Length / 2]) / 2;
            }
            else
            {
                // Odd number of elements
                median = compressionRatio[compressionRatio.Length / 2];
            }

            Console.WriteLine($"Median compression ratio: {median:F2}");

            // Compare the sizes of the vectors
            // The compressed versions should be at least 18% less than the original
            Assert.That(median > TestConstants.Compression.MinRatio512_768, "Compression ratio is not at least 18% less than the original");
        }


        [Test]
        public void TestCompress768()
        {
            var vectors = new List<Vector>();
            int[,] vectorSize = new int[TestConstants.Dimensions.Standard, 3];
            float[] compressionRatio = new float[TestConstants.Dimensions.Standard];

            // Create random Vectors of 768 size
            for (int i = 0; i < TestConstants.Dimensions.Standard; i++)
            {
                float[] floats = new float[TestConstants.Dimensions.Embedding768];
                for (int j = 0; j < TestConstants.Dimensions.Embedding768; j++)
                {
                    // Random float
                    floats[j] = (float)Random.Shared.NextDouble();
                }
                vectors.Add(new Vector(floats));

                // Gets size of vectors[i].Values in bytes
                vectorSize[i, 0] = vectors[i].Values.Length * sizeof(float);

                // Compress vectors[i].Values and report the compressed byte length
                vectorSize[i, 1] = vectors[i].ToCompressedBinary().Length;

                // Calculate the compression ratio
                compressionRatio[i] = (float)vectorSize[i, 0] / vectorSize[i, 1];
            }

            // Calculate the median compression ratio
            Array.Sort(compressionRatio);
            float median;
            if (compressionRatio.Length % 2 == 0)
            {
                // Even number of elements
                median = (compressionRatio[compressionRatio.Length / 2 - 1] + compressionRatio[compressionRatio.Length / 2]) / 2;
            }
            else
            {
                // Odd number of elements
                median = compressionRatio[compressionRatio.Length / 2];
            }

            Console.WriteLine($"Median compression ratio: {median:F2}");

            // Compare the sizes of the vectors
            // The compressed versions should be at least 18% less than the original
            Assert.That(median > TestConstants.Compression.MinRatio512_768, "Compression ratio is not at least 18% less than the original");
        }


        [Test]
        public void TestRoundTripCompression()
        {
            // Test that compress then decompress returns the original data
            float[] original = new float[TestConstants.Dimensions.Embedding768];
            for (int i = 0; i < TestConstants.Dimensions.Embedding768; i++)
            {
                original[i] = (float)Random.Shared.NextDouble();
            }

            var vector = new Vector(original);
            byte[] compressed = vector.ToCompressedBinary();
            Vector restored = Vector.FromCompressedBinary(compressed);

            Assert.That(restored.Values, Is.EqualTo(original), "Round-trip compression should preserve exact values");
        }


        [Test]
        public void TestRoundTripCompressionEmptyVector()
        {
            // Edge case: empty vector
            float[] original = Array.Empty<float>();
            var vector = new Vector(original);
            byte[] compressed = vector.ToCompressedBinary();
            Vector restored = Vector.FromCompressedBinary(compressed);

            Assert.That(restored.Values, Is.EqualTo(original), "Empty vector round-trip should work");
        }


        [Test]
        public void TestRoundTripCompressionSingleElement()
        {
            // Edge case: single element
            float[] original = new float[] { 3.14159f };
            var vector = new Vector(original);
            byte[] compressed = vector.ToCompressedBinary();
            Vector restored = Vector.FromCompressedBinary(compressed);

            Assert.That(restored.Values, Is.EqualTo(original), "Single element round-trip should work");
        }
    }
}
