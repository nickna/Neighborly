using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Neighborly.Tests.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {

            BenchmarkRunner.Run<VectorDatabaseAddVectors>();
        }
    }
    public class VectorDatabaseAddVectors
    {
        private Vector[][] vectors = null!;
        private VectorDatabase db = new();
        private readonly string[] words = 
        {
            "apple", "banana", "cherry", "date", "elderberry", "fig", "grape", "honeydew", "kiwi", "lemon", "mango", "nectarine", "orange", "pear", "quince", "raspberry", "strawberry", "tangerine", "watermelon",
            "apricot", "blackberry", "cantaloupe", "dragonfruit", "eggplant", "feijoa", "guava", "huckleberry", "jackfruit", "kumquat", "lychee", "mulberry", "olive", "papaya", "quince", "rhubarb", "starfruit", "tomato", "vanilla",
        };

        [GlobalSetup]
        public void Init()
        {
            vectors = new Vector[4][];
            vectors[0] = new Vector[256];
            vectors[1] = new Vector[256];
            vectors[2] = new Vector[256];
            vectors[3] = new Vector[256];

            // Make sure these match the [Arguments] attribute
            // Generate 256 random vectors at dimension 4096
            for (int i = 0; i < 256; i++)
            {
                vectors[0][i] = GenerateRandomVector(4096);
            }

            // Generate 256 random vectors at dimension 768
            for (int i = 0; i < 256; i++)
            {
                vectors[1][i] = GenerateRandomVector(768);
            }

            // Generate 256 random vectors at dimension 512
            for (int i = 0; i < 256; i++)
            {
                vectors[2][i] = GenerateRandomVector(512);
            }

            // Generate 256 random vectors at dimension 300
            for (int i = 0; i < 256; i++)
            {
                vectors[3][i] = GenerateRandomVector(300);
            }

        }

        [Benchmark]
        [Arguments(4096, 256, 0)]
        [Arguments(768, 256, 1)]
        [Arguments(512, 256, 2)]
        [Arguments(300, 256, 3)]

        public void AddVec(int dim, int amt, int vecarray)
        {
            for (int x = 0; x < amt; x++)
            {
                db.Vectors.Add(vectors[vecarray][1]);
            }
        }
        public Vector GenerateRandomVector(int Dimension)
        {
            var random = new Random();
            var values = new float[Dimension];
            string[] wordstr = new string[Dimension];
            for (int i = 0; i < Dimension; i++)
            {
                values[i] = random.NextSingle();
                wordstr[i] = words[random.Next(words.Length)];
            }

            return new Vector(values, string.Join(" ", wordstr));
        }
    }
}

