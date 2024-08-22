namespace Neighborly.Search;

public class KDTreeNode : IDataPersistence
{
    public required Vector Vector { get; set; }
    public KDTreeNode? Left { get; set; }
    public KDTreeNode? Right { get; set; }

    public void ToBinaryStream(BinaryWriter writer)
    {
        writer.Write(Vector.Id.ToByteArray());
        writer.Write(Left != null);
        Left?.ToBinaryStream(writer);
        writer.Write(Right != null);
        Right?.ToBinaryStream(writer);
    }

    internal static KDTreeNode? ReadFrom(BinaryReader reader, VectorList vectors, Span<byte> guidBuffer)
    {
        var vectorId = reader.ReadGuid(guidBuffer);
        var vector = vectors.GetById(vectorId);
        if (vector is null)
        {
            return null;
        }

        var left = reader.ReadBoolean() ? ReadFrom(reader, vectors, guidBuffer) : null;
        var right = reader.ReadBoolean() ? ReadFrom(reader, vectors, guidBuffer) : null;

        return new KDTreeNode
        {
            Vector = vector,
            Left = left,
            Right = right
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is not KDTreeNode other)
        {
            return false;
        }

        return other.Vector.Equals(Vector) &&
            (other.Left == null && Left == null || other.Left?.Equals(Left) == true) &&
            (other.Right == null && Right == null || other.Right?.Equals(Right) == true);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Vector, Left, Right);
    }

}
