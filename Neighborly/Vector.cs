using System.Text;
using Neighborly.Distance;

namespace Neighborly;

/// <summary>
/// Core data structure for representing a vector of floats.
/// </summary>
[Serializable]
public partial class Vector : IEquatable<Vector>, IDataPersistence
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
    public Vector(float[]? values = null, string? originalText = null, short[]? tags = null, VectorAttributes? vectorAttributes = null)
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

        Attributes = vectorAttributes ?? new VectorAttributes();
    }

    public Vector(BinaryReader stream)
    {
        // Read the Guid
        Id = new Guid(stream.ReadBytes(s_idBytesLength));

        OriginalText = stream.ReadString();

        // Read the Values array length, then create the values
        int valuesLength = stream.ReadInt32();
        Values = new float[valuesLength];
        for (int i = 0; i < valuesLength; i++)
        {
            Values[i] = stream.ReadSingle();
        }

        // Read the tags array length, then create the tags
        short tagsLength = stream.ReadInt16();
        Tags = new short[tagsLength];
        for (int i = 0; i < tagsLength; i++)
        {
            Tags[i] = stream.ReadInt16();
        }

        Attributes = new VectorAttributes(stream);
    }

    public Vector(byte[] byteArray)
        : this(byteArray.AsSpan())
    {
    }

    public Vector(ReadOnlySpan<byte> source) 
        : this(new BinaryReader(new MemoryStream(source.ToArray())))
    {
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
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        this.ToBinaryStream(writer);
        return stream.ToArray();
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

    public static IDataPersistence FromBinaryStream(BinaryReader reader)
    {
        throw new NotImplementedException();
    }

    public void ToBinaryStream(BinaryWriter writer)
    {
        writer.Write(Id.ToByteArray());
        writer.Write(OriginalText);
        writer.Write(Values.Length);
        foreach (float value in Values)
        {
            writer.Write(value);
        }
        writer.Write((short)Tags.Length);
        foreach (short tag in Tags)
        {
            writer.Write(tag);
        }
        Attributes.ToBinaryStream(writer);

    }
}
