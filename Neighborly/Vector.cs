namespace Neighborly;

/// <summary>
/// Core data structure for representing a vector of floats.
/// </summary>
[Serializable]
public class Vector
{
    /// <summary>
    /// Gets or sets the unique identifier of the vector.
    /// This is automatically created when the vector is initialized.
    /// </summary>
    public Guid Id { get; set; }
    public byte[] Values { get; }

    /// <summary>
    /// Initializes a new instance of the Vector class with the specified values.
    /// </summary>
    /// <param name="values">The array of bytes representing the values of the vector.</param>
    public Vector(byte[] values)
    {
        Values = values;
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// Gets the dimension of the vector.
    /// </summary>
    public int Dimension => Values.Length;

    /// <summary>
    /// Calculates the distance between this vector and another vector using the Euclidean distance.
    /// </summary>
    /// <param name="other">The other vector to calculate the distance to</param>
    /// <returns>The distance between the two vectors</returns>
    public float Distance(Vector other)
    {
        // Implement a distance metric, e.g., Euclidean distance
        float sum = 0;
        for (int i = 0; i < Dimension; i++)
        {
            float diff = Values[i] - other.Values[i];
            sum += diff * diff;
        }
        return (float)Math.Sqrt(sum);
    }

    /// <summary>
    /// Parses a byte array into a Vector object.
    /// This is used for deserialization and retrieval.
    /// </summary>
    /// <param name="vectorData">The byte array representing the vector data.</param>
    /// <returns>A new Vector object initialized with the parsed data.</returns>
    /// <seealso cref="ToBinary"/>
    internal static Vector Parse(byte[] vectorData)
    {
        return new Vector(vectorData);
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

        byte[] result = new byte[a.Dimension];
        for (int i = 0; i < a.Dimension; i++)
        {
            float sum = a[i] + b[i];
            Buffer.BlockCopy(BitConverter.GetBytes(sum), 0, result, i * sizeof(float), sizeof(float));
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
        byte[] result = new byte[a.Dimension];
        for (int i = 0; i < a.Dimension; i++)
        {
            float division = a[i] / n;
            Buffer.BlockCopy(BitConverter.GetBytes(division), 0, result, i * sizeof(float), sizeof(float));
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

        byte[] result = new byte[a.Dimension];
        for (int i = 0; i < a.Dimension; i++)
        {
            float difference = a[i] - b[i];
            Buffer.BlockCopy(BitConverter.GetBytes(difference), 0, result, i * sizeof(float), sizeof(float));
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
        get { return BitConverter.ToSingle(Values, index * sizeof(float)); }
        set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Values, index * sizeof(float), sizeof(float)); }
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
        return new Vector(data);
    }

    /// <summary>
    /// Gets the number of dimensions in the vector.
    /// </summary>
    public int Dimensions => Values.Length / sizeof(float);
}
