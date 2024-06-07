using System.Text;

namespace Neighborly;

/// <summary>
/// Core data structure for representing a vector of floats.
/// </summary>
[Serializable]
public partial class Vector
{
    /// <summary>
    /// Gets the unique identifier of the vector.
    /// This is automatically created when the vector is initialized.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Tags that associate the vector with a specific category or group.
    /// <seealso cref="VectorTags"/>
    /// </summary>
    public short[] Tags { get; }

    /// <summary>
    /// Embedding of the vector in a high-dimensional space.
    /// </summary>
    public float[] Values { get; }

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
        Tags = new short[0];
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
        Tags = new short[0];

    }

    public Vector(BinaryReader stream)
    {
        // Read the Guid
        byte[] idBytes = stream.ReadBytes(16);
        Guid id = new Guid(idBytes);

        // Read the length of the values array
        int valuesLength = stream.ReadInt32();

        // Read the length of the original text
        int originalTextLength = stream.ReadInt32();

        // Read the original text
        byte[] originalTextBytes = stream.ReadBytes(originalTextLength);
        string originalText = Encoding.UTF8.GetString(originalTextBytes);

        // Read the values
        float[] values = new float[valuesLength];
        for (int i = 0; i < valuesLength; i++)
        {
            values[i] = stream.ReadSingle();
        }

        // Read the length of the tags array
        int tagsLength = stream.ReadInt16();
        short[] tags = new short[tagsLength];
        for (int i = 0; i < tagsLength; i++)
        {
            tags[i] = stream.ReadInt16();
        }

        // Assign the read values to the properties
        Id = id;
        Values = values;
        OriginalText = originalText;
    }

    public Vector(byte[] byteArray)
    {
        var id = new Guid(byteArray.Take(16).ToArray());
        var valuesLength = BitConverter.ToInt32(byteArray, 16);
        var originalTextLength = BitConverter.ToInt32(byteArray, 16 + sizeof(int));
        var originalText = Encoding.UTF8.GetString(byteArray, 16 + sizeof(int) + sizeof(int), originalTextLength);
        var values = new float[valuesLength];
        var tagsLength = BitConverter.ToInt16(byteArray,16 + sizeof(int) + sizeof(int) + originalTextLength);
        var tags = new short[tagsLength];
        for (int i = 0; i < tagsLength; i++)
        {
            tags[i] = BitConverter.ToInt16(byteArray, 16 + sizeof(int) + sizeof(int) + originalTextLength + sizeof(int) + (i * sizeof(short)));
        }
        for (int i = 0; i < valuesLength; i++)
        {
            values[i] = BitConverter.ToSingle(byteArray, 16 + sizeof(int) + sizeof(int) + originalTextLength + sizeof(short) + (tagsLength * sizeof(short)) + (i * sizeof(float)));
        }
        Id = id;
        Values = values;
        OriginalText = originalText;
        Tags = tags;
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
        byte[] idBytes = Id.ToByteArray();
        byte[] valuesLengthBytes = BitConverter.GetBytes(Values.Length);
        byte[] originalTextLengthBytes = BitConverter.GetBytes(OriginalText.Length);
        byte[] originalTextBytes = Encoding.UTF8.GetBytes(OriginalText);
        byte[] valuesBytes = new byte[Values.Length * sizeof(float)];
        for (int i = 0; i < Values.Length; i++)
        {
            byte[] bytes = BitConverter.GetBytes(Values[i]);
            Array.Copy(bytes, 0, valuesBytes, i * sizeof(float), sizeof(float));
        }
        byte[] tagsLengthBytes = BitConverter.GetBytes(Tags.Length);
        byte[] tagsBytes = new byte[Tags.Length * sizeof(short)];
        for (int i = 0; i < Tags.Length; i++)
        {
            byte[] bytes = BitConverter.GetBytes(Tags[i]);
            Array.Copy(bytes, 0, tagsBytes, i * sizeof(short), sizeof(short));
        }
        return idBytes.Concat(valuesLengthBytes).Concat(originalTextLengthBytes).Concat(originalTextBytes).Concat(valuesBytes).Concat(tagsLengthBytes).Concat(tagsBytes).ToArray();
    }

    /// <summary>
    /// Gets the number of dimensions in the vector.
    /// </summary>
    public int Dimensions => Values.Length / sizeof(float);
}
