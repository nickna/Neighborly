using System.Text;
using Neighborly.Distance;

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
    /// Attributes that associate this vector to a specific user or organization.
    /// Also used to determine storage priority (e.g., cache, disk, etc.)
    /// </summary>
    public VectorAttributes Attributes { get; set; }

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
    /// <example>
    /// var vec = new Vector("The quick brown fox jumps over the lazy dog"); // This will generate the embedding (Values) for the text
    /// var vec2 = new Vector(new float[] { 1.0f, 2.0f, 3.0f }); // This will use the provided values
    /// </example>
    /// <param name="values">The array of float values representing the vector</param>
    /// <param name="originalText">human-readable text that relates to the Values (aka embeddings)</param>
    /// <param name="tags">metadata that can help identify and categorize content</param>
    public Vector(float[]? values = null, string? originalText = null, short[]? tags = null)
    {
        if (values == null && originalText == null)
        {
            throw new ArgumentException("Either values or originalText must be provided.");
        }

        if (values != null)
        {
            Values = values;
            OriginalText = originalText ?? string.Empty;
        }
        else
        {
            Values = EmbeddingGenerator.Instance.GenerateEmbedding(originalText!);
            OriginalText = originalText!;
        }

        Id = Guid.NewGuid();
        
        if (tags == null)
            Tags = Array.Empty<short>();
        else
            Tags = tags;
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

        // Read the VectorAttributes
        VectorAttributes attributes = new VectorAttributes(stream);

        Tags = tags;
        Id = id;
        Values = values;
        OriginalText = originalText;
        Attributes = attributes;
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

        int attributesOffset = tagsOffset + (tagsLength * sizeof(short));
        var attributes = new VectorAttributes(new BinaryReader(new MemoryStream(source[attributesOffset..].ToArray())));

        Id = id;
        Values = values;
        OriginalText = originalText;
        Tags = tags;
        Attributes = attributes;
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
    /// <param name="other">The other vector to calculate the distance to</param>
    /// <param name="vectorDistanceMeasurement">The distance algorithm to use. Default is Euclidian</param>
    /// <returns>The distance between the two vectors</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="other"/> is null.</exception>
    public float Distance(Vector other, IDistanceCalculator? distanceCalculator = null)
    {
        ArgumentNullException.ThrowIfNull(other);

        distanceCalculator ??= EuclideanDistanceCalculator.Instance;
        return distanceCalculator.CalculateDistance(this, other);
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
        byte[] attributesBytes = Attributes.ToBinary();
        int attributesBytesLength = attributesBytes.Length;

        int valuesBytesLength = Values.Length * sizeof(float);
        int tagsBytesLength = Tags.Length * sizeof(short);
        int originalTextBytesLength = Encoding.UTF8.GetByteCount(OriginalText);
        int resultLength = s_idBytesLength + s_valuesLengthBytesLength + s_originalTextLengthBytesLength + originalTextBytesLength + valuesBytesLength + s_tagsLengthBytesLength + tagsBytesLength + attributesBytesLength;

        int valuesOffset = s_originalTextOffset + originalTextBytesLength;
        int tagsLengthOffset = valuesOffset + valuesBytesLength;
        int tagsOffset = tagsLengthOffset + s_tagsLengthBytesLength;
        int attributesOffset = tagsOffset + tagsBytesLength;

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
        if (!BitConverter.TryWriteBytes(originalTextLengthBytes, originalTextBytesLength))
        {
            throw new InvalidOperationException("Failed to write originalTextBytesLength to bytes");
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

        Span<byte> attributesBytesSpan = result[attributesOffset..(attributesOffset + attributesBytesLength)];
        attributesBytes.CopyTo(attributesBytesSpan);

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
    public int Dimensions => Values.Length;

    private void GuardDimensionsMatch(Vector other) => GuardDimensionsMatch(this, other);

    internal static void GuardDimensionsMatch(Vector a, Vector b)
    {
        if (a.Dimension != b.Dimension)
        {
            throw new ArgumentException("Dimensions must match");
        }
    }
}
