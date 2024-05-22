namespace Neighborly;

[Serializable]
public class Vector
{
    public Guid Id { get; set; }
    public byte[] Values { get; }

    public Vector(byte[] values)
    {
        Values = values;
        Id = Guid.NewGuid();
    }

    public int Dimension => Values.Length;

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

    internal static Vector Parse(byte[] vectorData)
    {
        return new Vector(vectorData);
    }


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


    public float this[int index]
    {
        get { return BitConverter.ToSingle(Values, index * sizeof(float)); }
        set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, Values, index * sizeof(float), sizeof(float)); }
    }

    public float Magnitude
    {
        get { return (float)Math.Sqrt(Values.Sum(x => x * x)); }
    }

    public byte[] ToBinary()
    {
        byte[] binaryData = new byte[Values.Length * sizeof(float)];
        Buffer.BlockCopy(Values, 0, binaryData, 0, binaryData.Length);
        return binaryData;
    }

    public static Vector FromBinary(byte[] data)
    {
        return new Vector(data);
    }

    public int Dimensions => Values.Length / sizeof(float);
}
