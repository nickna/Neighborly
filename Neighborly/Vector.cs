namespace Neighborly;

/// <summary>
/// Core data structure for representing a vector of floats.
/// </summary>
[Serializable]
public partial class Vector
{
    /// <summary>
    /// Gets or sets the unique identifier of the vector.
    /// This is automatically created when the vector is initialized.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Embedding of the vector in a high-dimensional space.
    /// </summary>
    public float[] Values { get; }

    /// <summary>
    /// Returns vector as a byte array.
    /// (This is used for serialization and storage.)
    /// </summary>
    public byte[] GetBinaryValues() 
    { 
        return ToBinary(); 
    }

    /// <summary>
    /// The text that belongs to the vector.
    /// </summary>
    public string OriginalText { get; }

    /// <summary>
    /// Initializes a new instance of the Vector class with the specified values.
    /// </summary>
    /// <param name="values">The array of float values representing the vector</param>
    public Vector(float[] values)
    {
        Values = values;
        Id = Guid.NewGuid();
        OriginalText = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the Vector class with the specified values and text.
    /// </summary>
    /// <param name="values">The array of float values representing the vector</param>
    /// <param name="originalText"></param>
    public Vector(float[] values, string originalText)
    {
        Values = values;
        OriginalText = originalText;
        Id = Guid.NewGuid();
    }

    public Vector(byte[] byteArray)
    {
        // Convert the byte array to a float array
        float[] values = new float[byteArray.Length / sizeof(float)];
        Buffer.BlockCopy(byteArray, 0, values, 0, byteArray.Length);

        // Call the existing constructor
        Values = values;
        Id = Guid.NewGuid();
        OriginalText = string.Empty;
    }

    public Vector(byte[] byteArray, string originalText)
    {
        // Convert the byte array to a float array
        float[] values = new float[byteArray.Length / sizeof(float)];
        Buffer.BlockCopy(byteArray, 0, values, 0, byteArray.Length);

        // Call the existing constructor
        Values = values;
        OriginalText = originalText;
        Id = Guid.NewGuid();
    }


    /// <summary>
    /// Gets the dimension of the vector.
    /// </summary>
    public int Dimension => Values.Length;

    /// <summary>
    /// Calculates the distance between this vector and another vector 
    /// </summary>
    /// <param name="vectorDistanceMeasurement">The distance algorithm to use. Default is Euclidian</param>
    /// <param name="other">The other vector to calculate the distance to</param>
    /// <returns>The distance between the two vectors</returns>
    public float Distance(Vector other, VectorDistanceMeasurement vectorDistanceMeasurement = VectorDistanceMeasurement.EuclideanDistance)
    {
        switch (vectorDistanceMeasurement)
        {
            case VectorDistanceMeasurement.EuclideanDistance:
                {
                    // Calculate distance using Euclidean math
                    float sum = 0;
                    for (int i = 0; i < Dimension; i++)
                    {
                        float diff = Values[i] - other.Values[i];
                        sum += diff * diff;
                    }
                    return (float)Math.Sqrt(sum);
                }
            case VectorDistanceMeasurement.ManhattanDistance:
                {
                    // Calculate distance metric using Manhattan distance
                    float sum = 0;
                    for (int i = 0; i < Dimension; i++)
                    {
                        float diff = Values[i] - other.Values[i];
                        sum += Math.Abs(diff);
                    }
                    return sum;
                }
            case VectorDistanceMeasurement.ChebyshevDistance:
                {
                    // Calculate distance metric using Chebyshev distance
                    float max = 0;
                    for (int i = 0; i < Dimension; i++)
                    {
                        float diff = Math.Abs(Values[i] - other.Values[i]);
                        if (diff > max)
                        {
                            max = diff;
                        }
                    }
                    return max;
                }
            case VectorDistanceMeasurement.MinkowskiDistance:
                {
                    // Calculate distance metric using Minkowski distance
                    float sum = 0;
                    for (int i = 0; i < Dimension; i++)
                    {
                        float diff = Values[i] - other.Values[i];
                        sum += (float)Math.Pow(Math.Abs(diff), 3);
                    }
                    return (float)Math.Pow(sum, 1.0 / 3.0);
                }
            case VectorDistanceMeasurement.CosineSimilarity:
                {
                    // Calculate distance metric using Cosine similarity
                    float dotProduct = 0;
                    float magnitudeA = 0;
                    float magnitudeB = 0;
                    for (int i = 0; i < Dimension; i++)
                    {
                        dotProduct += Values[i] * other.Values[i];
                        magnitudeA += Values[i] * Values[i];
                        magnitudeB += other.Values[i] * other.Values[i];
                    }
                    magnitudeA = (float)Math.Sqrt(magnitudeA);
                    magnitudeB = (float)Math.Sqrt(magnitudeB);
                    return dotProduct / (magnitudeA * magnitudeB);
                }
            default:
                {
                    throw new ArgumentException("Invalid distance measurement");
                }
        }
        
    }

    /// <summary>
    /// Adds two vectors element-wise.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>A new vector representing the element-wise sum of the two vectors.</returns>
    /// <exception cref="ArgumentException">Thrown when the dimensions of the vectors do not match.</exception>
    public static Vector operator +(Vector a, Vector b)
    {
        if (a.Dimension != b.Dimension)
            throw new ArgumentException("Dimensions must match");

        float[] result = new float[a.Dimension];
        for (int i = 0; i < a.Dimension; i++)
        {
            result[i] = a[i] + b[i];
        }
        return new Vector(result);
    }

    /// <summary>
    /// Divides each element of a vector by a scalar value.
    /// </summary>
    /// <param name="a">The vector to divide.</param>
    /// <param name="n">The scalar value to divide by.</param>
    /// <returns>A new vector representing the element-wise division of the vector by the scalar.</returns>
    public static Vector operator /(Vector a, int n)
    {
        float[] result = new float[a.Dimension];
        for (int i = 0; i < a.Dimension; i++)
        {
            result[i] = a[i] / n;
        }
        return new Vector(result);
    }

    /// <summary>
    /// Subtracts two vectors element-wise.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>A new vector representing the element-wise difference of the two vectors.</returns>
    /// <exception cref="ArgumentException">Thrown when the dimensions of the vectors do not match.</exception>
    public static Vector operator -(Vector a, Vector b)
    {
        if (a.Dimension != b.Dimension)
            throw new ArgumentException("Dimensions must match");

        float[] result = new float[a.Dimension];
        for (int i = 0; i < a.Dimension; i++)
        {
            result[i] = a[i] - b[i];
        }
        return new Vector(result);
    }

    /// <summary>
    /// Gets or sets the value at the specified index in the vector.
    /// </summary>
    /// <param name="index">The index of the value to get or set.</param>
    /// <returns>The value at the specified index.</returns>
    public float this[int index]
    {
        get { return Values[index]; }
        set { Values[index] = value; }
    }

    /// <summary>
    /// Gets the magnitude (length) of the vector.
    /// </summary>
    public float Magnitude
    {
        get { return (float)Math.Sqrt(Values.Sum(x => x * x)); }
    }


    /// <summary>
    /// Converts the vector to a binary representation.
    /// This is used for serialization and storage.
    /// </summary>
    /// <returns>The binary representation of the vector.</returns>
    /// <seealso cref="Parse"/>"/>
    public byte[] ToBinary()
    {
        byte[] binaryData = new byte[Values.Length * sizeof(float)];
        Buffer.BlockCopy(Values, 0, binaryData, 0, binaryData.Length);
        return binaryData;
    }

    /// <summary>
    /// Creates a new Vector object from a binary representation.
    /// This is used for deserialization and retrieval.
    /// </summary>
    /// <param name="data">The binary data representing the vector.</param>
    /// <returns>A new Vector object initialized with the binary data.</returns>
    public static Vector FromBinary(byte[] data)
    {
        float[] values = new float[data.Length / sizeof(float)];
        Buffer.BlockCopy(data, 0, values, 0, data.Length);
        return new Vector(values);
    }


    /// <summary>
    /// Gets the number of dimensions in the vector.
    /// </summary>
    public int Dimensions => Values.Length / sizeof(float);
}
