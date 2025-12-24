namespace Neighborly.Search;

/// <summary>
/// Represents a node in the mutable Ball-tree.
/// Uses record for value-based equality while allowing mutable Left/Right/Radius for tree construction.
/// </summary>
public sealed record BallTreeNode
{
    public required Vector Center { get; init; }
    public double Radius { get; set; }
    public BallTreeNode? Left { get; set; }
    public BallTreeNode? Right { get; set; }

    internal void WriteTo(BinaryWriter writer)
    {
        writer.Write(Center.Id.ToByteArray());
        writer.Write(Radius);
        writer.Write(Left != null);
        Left?.WriteTo(writer);
        writer.Write(Right != null);
        Right?.WriteTo(writer);
    }

    internal static BallTreeNode? ReadFrom(BinaryReader reader, VectorList vectors, VectorDatabase internalVectors, Span<byte> guidBuffer)
    {
        var centerId = reader.ReadGuid(guidBuffer);
        var center = internalVectors.Vectors.GetById(centerId) ?? vectors.GetById(centerId);
        if (center is null)
        {
            return null;
        }

        var radius = reader.ReadDouble();
        var left = reader.ReadBoolean() ? ReadFrom(reader, vectors, internalVectors, guidBuffer) : null;
        var right = reader.ReadBoolean() ? ReadFrom(reader, vectors, internalVectors, guidBuffer) : null;

        return new BallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = left,
            Right = right
        };
    }

    internal static BallTreeNode? ReadFrom(BinaryReader reader, VectorList vectors, VectorList internalVectors, Span<byte> guidBuffer)
    {
        var centerId = reader.ReadGuid(guidBuffer);
        var center = internalVectors.GetById(centerId) ?? vectors.GetById(centerId);
        if (center is null)
        {
            return null;
        }

        var radius = reader.ReadDouble();
        var left = reader.ReadBoolean() ? ReadFrom(reader, vectors, internalVectors, guidBuffer) : null;
        var right = reader.ReadBoolean() ? ReadFrom(reader, vectors, internalVectors, guidBuffer) : null;

        return new BallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = left,
            Right = right
        };
    }
}
