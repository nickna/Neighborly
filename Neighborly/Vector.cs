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
        byte[] idBytes = stream.ReadBytes(s_idBytesLength);
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

        Tags = tags;
        Id = id;
        Values = values;
        OriginalText = originalText;
        Tags = tags;
    }

    public Vector(byte[] byteArray)
        : this(byteArray.AsSpan())
    {
    }

    public Vector(ReadOnlySpan<byte> source)
    {
        var id = new Guid(source[..s_idBytesLength]);
        var valuesLength = BitConverter.ToInt32(source[s_valuesLengthOffset..(s_valuesLengthOffset + s_valuesLengthBytesLength)]);
        var originalTextBytesLength = BitConverter.ToInt32(source[s_originalTextLengthOffset..(s_originalTextLengthOffset + s_originalTextLengthBytesLength)]);

        int valuesOffset = s_originalTextOffset + originalTextBytesLength;

        var originalText = Encoding.UTF8.GetString(source[s_originalTextOffset..(s_originalTextOffset + originalTextBytesLength)]);
        var values = new float[valuesLength];

        var valuesBytesLength = valuesLength * sizeof(float);
        var valuesSource = source[valuesOffset..(valuesOffset + valuesBytesLength)];
        for (int i = 0; i < valuesLength; i++)
        {
            values[i] = BitConverter.ToSingle(valuesSource[(i * sizeof(float))..((i + 1) * sizeof(float))]);
        }

        int tagsLengthOffset = valuesOffset + valuesBytesLength;
        int tagsOffset = tagsLengthOffset + s_tagsLengthBytesLength;
        var tagsLength = BitConverter.ToInt16(source[tagsLengthOffset..(tagsLengthOffset + s_tagsLengthBytesLength)]);
        var tagsSource = source[tagsOffset..(tagsOffset + (tagsLength * sizeof(short)))];
        var tags = new short[tagsLength];
        for (int i = 0; i < tagsLength; i++)
        {
            tags[i] = BitConverter.ToInt16(tagsSource[(i * sizeof(short))..((i + 1) * sizeof(short))]);
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
        int valuesBytesLength = Values.Length * sizeof(float);
        int tagsBytesLength = Tags.Length * sizeof(short);
        int originalTextBytesLength = Encoding.UTF8.GetByteCount(OriginalText);
        int resultLength = s_idBytesLength + s_valuesLengthBytesLength + s_originalTextLengthBytesLength + originalTextBytesLength + valuesBytesLength + s_tagsLengthBytesLength + tagsBytesLength;

        int valuesOffset = s_originalTextOffset + originalTextBytesLength;
        int tagsLengthOffset = valuesOffset + valuesBytesLength;
        int tagsOffset = tagsLengthOffset + s_tagsLengthBytesLength;

        Span<byte> result = stackalloc byte[resultLength];
        Span<byte> idBytes = result[..s_idBytesLength];
        if (!Id.TryWriteBytes(idBytes))
        {
            throw new InvalidOperationException("Failed to write the Id to bytes");
        }

        Span<byte> valuesLengthBytes = result[s_valuesLengthOffset..(s_valuesLengthOffset + s_valuesLengthBytesLength)];
        if (!BitConverter.TryWriteBytes(valuesLengthBytes, Values.Length))
        {
            throw new InvalidOperationException("Failed to write Values.Length to bytes");
        }

        Span<byte> tagsLengthBytes = result[tagsLengthOffset..(tagsLengthOffset + s_tagsLengthBytesLength)];
        if (!BitConverter.TryWriteBytes(tagsLengthBytes, (short)Tags.Length))
        {
            throw new InvalidOperationException("Failed to write Tags.Length to bytes");
        }

        Span<byte> originalTextLengthBytes = result[s_originalTextLengthOffset..(s_originalTextLengthOffset + s_originalTextLengthBytesLength)];
        if (!BitConverter.TryWriteBytes(originalTextLengthBytes, OriginalText.Length))
        {
            throw new InvalidOperationException("Failed to write OriginalText.Length to bytes");
        }

        Span<byte> originalTextBytes = result[s_originalTextOffset..(s_originalTextOffset + originalTextBytesLength)];
        if (!Encoding.UTF8.TryGetBytes(OriginalText, originalTextBytes, out int bytesWritten))
        {
            throw new InvalidOperationException("Failed to write OriginalText to bytes");
        }

        Span<byte> valuesBytes = result[valuesOffset..(valuesOffset + valuesBytesLength)];
        for (int i = 0; i < Values.Length; i++)
        {
            if (!BitConverter.TryWriteBytes(valuesBytes[(i * sizeof(float))..], Values[i]))
            {
                throw new InvalidOperationException($"Failed to write Value[{i}] to bytes");
            }
        }

        Span<byte> tagsBytes = result[tagsOffset..(tagsOffset + tagsBytesLength)];
        for (int i = 0; i < Tags.Length; i++)
        {
            if (!BitConverter.TryWriteBytes(tagsBytes[(i * sizeof(short))..], Tags[i]))
            {
                throw new InvalidOperationException($"Failed to write Value[{i}] to bytes");
            }
        }

        return result.ToArray();
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
