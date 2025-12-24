namespace Neighborly.Search;

/// <summary>
/// Represents a node in the mutable KD-tree.
/// Uses record for value-based equality while allowing mutable Left/Right for tree construction.
/// </summary>
public sealed record KDTreeNode
{
    public required Vector Vector { get; init; }
    public KDTreeNode? Left { get; set; }
    public KDTreeNode? Right { get; set; }

    public bool IsLeaf => Left is null && Right is null;

    internal void WriteTo(BinaryWriter writer)
    {
        writer.Write(Vector.Id.ToByteArray());
        writer.Write(Left != null);
        Left?.WriteTo(writer);
        writer.Write(Right != null);
        Right?.WriteTo(writer);
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
}
