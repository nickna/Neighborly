using System.Text;

namespace Neighborly;

/// <summary>
/// Core data structure for representing a vector of floats.
/// </summary>
[Serializable]
public partial class Vector : IEquatable<Vector>
{
    private const int s_idBytesLength = 16;
    private const int s_valuesLengthBytesLength = sizeof(int);
    private const int s_tagsLengthBytesLength = sizeof(short);
    private const int s_originalTextLengthBytesLength = sizeof(int);

    private const int s_valuesLengthOffset = s_idBytesLength;
    private const int s_originalTextLengthOffset = s_valuesLengthOffset + s_valuesLengthBytesLength;
    private const int s_originalTextOffset = s_originalTextLengthOffset + s_originalTextLengthBytesLength;

    /// <summary>
    /// Gets the unique identifier of the vector.
    /// This is automatically created when the vector is initialized.
    /// </summary>
    public Guid Id { get; internal set; }

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
    /// Numerical precision of the vector. This affects the size of the binary representation.
    /// Full = 32-bit float, Half = 16-bit float, Quantized8 = 8-bit quantized integer
    /// </summary>
    public VectorPrecision Precision { get; set; }

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
        Precision = VectorPrecision.Full;
    }

    /// <summary>
    /// Initializes a new instance of the Vector class with the specified values and text.
    /// </summary>
    /// <param name="values">The array of float values representing the vector</param>
    /// <param name="originalText"></param>
    public Vector(float[] values, string originalText, VectorPrecision precision = VectorPrecision.Full)
    {
        Values = values;
        OriginalText = originalText;
        Id = Guid.NewGuid();
        Tags = new short[0];
        Precision = precision;
    }

    public Vector(BinaryReader stream)
    {
        Precision = (VectorPrecision)stream.ReadByte();
        Id = new Guid(stream.ReadBytes(s_idBytesLength));
        int valuesLength = stream.ReadInt32();  // Read the number of float values
        int originalTextLength = stream.ReadInt32();
        OriginalText = Encoding.UTF8.GetString(stream.ReadBytes(originalTextLength));
        int compressedValuesLength = stream.ReadInt32();  // Read the length of compressed data
        byte[] compressedValues = stream.ReadBytes(compressedValuesLength);
        Values = Decompress(Precision, compressedValues);

        if (Values.Length != valuesLength)
        {
            throw new InvalidOperationException($"Decompressed Values length ({Values.Length}) does not match the expected length ({valuesLength})");
        }

        short tagsLength = stream.ReadInt16();
        Tags = new short[tagsLength];
        for (int i = 0; i < tagsLength; i++)
        {
            Tags[i] = stream.ReadInt16();
        }
    }

    public Vector(byte[] byteArray)
        : this(byteArray.AsSpan())
    {
    }

    public Vector(ReadOnlySpan<byte> source)
    {
        int currentOffset = 0;

        // Read Precision
        Precision = (VectorPrecision)source[currentOffset];
        currentOffset += sizeof(byte);

        // Read Id
        Id = new Guid(source.Slice(currentOffset, s_idBytesLength));
        currentOffset += s_idBytesLength;

        // Read Values length (number of float values, not compressed byte length)
        int valuesLength = BitConverter.ToInt32(source.Slice(currentOffset, sizeof(int)));
        currentOffset += sizeof(int);

        // Read OriginalText length
        int originalTextBytesLength = BitConverter.ToInt32(source.Slice(currentOffset, sizeof(int)));
        currentOffset += sizeof(int);

        // Read OriginalText
        OriginalText = Encoding.UTF8.GetString(source.Slice(currentOffset, originalTextBytesLength));
        currentOffset += originalTextBytesLength;

        // Read compressed Values length
        int compressedValuesLength = BitConverter.ToInt32(source.Slice(currentOffset, sizeof(int)));
        currentOffset += sizeof(int);

        // Read compressed Values
        byte[] compressedValues = source.Slice(currentOffset, compressedValuesLength).ToArray();
        currentOffset += compressedValuesLength;

        // Decompress Values
        Values = Decompress(Precision, compressedValues);

        // Verify that the decompressed Values length matches the expected length
        if (Values.Length != valuesLength)
        {
            throw new InvalidOperationException($"Decompressed Values length ({Values.Length}) does not match the expected length ({valuesLength})");
        }

        // Read Tags length
        short tagsLength = BitConverter.ToInt16(source.Slice(currentOffset, sizeof(short)));
        currentOffset += sizeof(short);

        // Read Tags
        Tags = new short[tagsLength];
        for (int i = 0; i < tagsLength; i++)
        {
            Tags[i] = BitConverter.ToInt16(source.Slice(currentOffset, sizeof(short)));
            currentOffset += sizeof(short);
        }
    }


    internal Vector(Guid id, float[] values, short[] tags, string? originalText)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(tags);

        Id = id;
        Values = values;
        Tags = tags;
        OriginalText = originalText ?? string.Empty;
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
        GuardDimensionsMatch(a, b);

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
        GuardDimensionsMatch(a, b);

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
    /// Adds the <paramref name="addend"/> vector to this vector element-wise.
    /// </summary>
    /// <param name="addend">The vector to add.</param>
    /// <exception cref="ArgumentException">Thrown when the dimensions of the vectors do not match.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="addend"/> is null.</exception>
    public void InPlaceAdd(Vector addend)
    {
        ArgumentNullException.ThrowIfNull(addend);
        GuardDimensionsMatch(addend);

        for (int i = 0; i < Dimension; i++)
        {
            Values[i] = Values[i] + addend[i];
        }
    }

    /// <summary>
    /// Subtracts the <paramref name="subtrahend"/> vector from this vector element-wise.
    /// </summary>
    /// <param name="subtrahend">The vector to subtract.</param>
    /// <exception cref="ArgumentException">Thrown when the dimensions of the vectors do not match.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="subtrahend"/> is null.</exception>
    public void InPlaceSubtract(Vector subtrahend)
    {
        ArgumentNullException.ThrowIfNull(subtrahend);
        GuardDimensionsMatch(subtrahend);

        for (int i = 0; i < Dimension; i++)
        {
            Values[i] = Values[i] - subtrahend[i];
        }
    }

    /// <summary>
    /// Divides each element of this vector by a scalar value.
    /// </summary>
    /// <param name="n">The scalar value to divide by.</param>
    public void InPlaceDivide(int n)
    {
        for (int i = 0; i < Dimension; i++)
        {
            Values[i] = Values[i] / n;
        }
    }

    /// <summary>
    /// Converts the vector to a binary representation.
    /// This is used for serialization and storage.
    /// </summary>
    /// <returns>The binary representation of the vector.</returns>
    /// <seealso cref="Parse"/>"/>
    public byte[] ToBinary()
    {
        byte[] compressedValues = Compress(Precision, Values);
        int tagsBytesLength = Tags.Length * sizeof(short);
        int originalTextBytesLength = Encoding.UTF8.GetByteCount(OriginalText);
        int resultLength = sizeof(byte) + s_idBytesLength + sizeof(int) + sizeof(int) + originalTextBytesLength + sizeof(int) + compressedValues.Length + sizeof(short) + tagsBytesLength;

        using var memoryStream = new MemoryStream(resultLength);
        using var writer = new BinaryWriter(memoryStream);

        writer.Write((byte)Precision);
        writer.Write(Id.ToByteArray());
        writer.Write(Values.Length);  // Write the number of float values
        writer.Write(originalTextBytesLength);
        writer.Write(Encoding.UTF8.GetBytes(OriginalText));
        writer.Write(compressedValues.Length);  // Write the length of compressed data
        writer.Write(compressedValues);
        writer.Write((short)Tags.Length);
        foreach (short tag in Tags)
        {
            writer.Write(tag);
        }

        return memoryStream.ToArray();
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is not Vector other)
        {
            return false;
        }

        return Equals(this, other);
    }

    /// <inheritdoc/>
    bool IEquatable<Vector>.Equals(Vector? other)
    {
        if (other == null)
        {
            return false;
        }

        return Equals(this, other);
    }

    private static bool Equals(Vector a, Vector b)
    {
        if (a.Dimension != b.Dimension)
        {
            return false;
        }

        for (int i = 0; i < a.Dimension; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // TODO: Define if Guid, OriginalText, or Tags should be included in the hash code
        return HashCode.Combine(Values);
    }

    /// <summary>
    /// Gets the number of dimensions in the vector.
    /// </summary>
    public int Dimensions => Values.Length / sizeof(float);

    private void GuardDimensionsMatch(Vector other) => GuardDimensionsMatch(this, other);

    private static void GuardDimensionsMatch(Vector a, Vector b)
    {
        if (a.Dimensions != b.Dimensions)
        {
            throw new ArgumentException("Dimensions must match");
        }
    }
}
