using Neighborly;
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
            int[,] vectorSize = new int[128,3];
            float[] compressionRatio= new float[128];

            // Create random Vectors of 4k size
            for (int i = 0; i < 128; i++)
            {
                float[] floats = new float[4096];
                for (int j = 0; j < 4096; j++)
                {
                    // Random float
                    floats[j] = (float)Random.Shared.NextDouble();
                }
                vectors.Add(new Vector(floats));


                // Gets size of vectors[i].Values in bytes
                vectorSize[i, 0] = vectors[i].Values.Length * sizeof(float);

                // Compress vectors[i].Values and report the compressed byte length
                vectorSize[i, 1] = vectors[i].ToCompressedBinary().Length;

                // Calculte the compression ratio
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
            Assert.That(median > 1.2f, "Compression ratio is not at least 20% less than the original");


        }


        [Test]
        public void TestCompress512()
        {
            var vectors = new List<Vector>();
            int[,] vectorSize = new int[128, 3];
            float[] compressionRatio = new float[128];

            // Create random Vectors of 4k size
            for (int i = 0; i < 128; i++)
            {
                float[] floats = new float[512];
                for (int j = 0; j < 512; j++)
                {
                    // Random float
                    floats[j] = (float)Random.Shared.NextDouble();
                }
                vectors.Add(new Vector(floats));


                // Gets size of vectors[i].Values in bytes
                vectorSize[i, 0] = vectors[i].Values.Length * sizeof(float);

                // Compress vectors[i].Values and report the compressed byte length
                vectorSize[i, 1] = vectors[i].ToCompressedBinary().Length;

                // Calculte the compression ratio
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
            Assert.That(median > 1.18f, "Compression ratio is not at least 18% less than the original");


        }


        [Test]
        public void TestCompress768()
        {
            var vectors = new List<Vector>();
            int[,] vectorSize = new int[128, 3];
            float[] compressionRatio = new float[128];

            // Create random Vectors of 4k size
            for (int i = 0; i < 128; i++)
            {
                float[] floats = new float[768];
                for (int j = 0; j < 768; j++)
                {
                    // Random float
                    floats[j] = (float)Random.Shared.NextDouble();
                }
                vectors.Add(new Vector(floats));


                // Gets size of vectors[i].Values in bytes
                vectorSize[i, 0] = vectors[i].Values.Length * sizeof(float);

                // Compress vectors[i].Values and report the compressed byte length
                vectorSize[i, 1] = vectors[i].ToCompressedBinary().Length;

                // Calculte the compression ratio
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
            Assert.That(median > 1.18f, "Compression ratio is not at least 18% less than the original");


        }
    }
}
