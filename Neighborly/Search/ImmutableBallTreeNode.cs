namespace Neighborly.Search;

/// <summary>
/// Immutable Ball-tree node supporting structural sharing and lock-free reads.
/// Uses record semantics for value-based equality and immutability.
/// </summary>
public sealed record ImmutableBallTreeNode
{
    /// <summary>
    /// The center vector of this ball (bounding sphere).
    /// </summary>
    public required Vector Center { get; init; }

    /// <summary>
    /// The radius of this ball (bounding sphere).
    /// </summary>
    public required double Radius { get; init; }

    /// <summary>
    /// The left child node.
    /// </summary>
    public ImmutableBallTreeNode? Left { get; init; }

    /// <summary>
    /// The right child node.
    /// </summary>
    public ImmutableBallTreeNode? Right { get; init; }

    /// <summary>
    /// Returns true if this node has no children.
    /// </summary>
    public bool IsLeaf => Left is null && Right is null;

    /// <summary>
    /// Writes this node and its subtree to a binary writer.
    /// </summary>
    internal void WriteTo(BinaryWriter writer)
    {
        writer.Write(Center.Id.ToByteArray());
        writer.Write(Radius);
        writer.Write(Left is not null);
        Left?.WriteTo(writer);
        writer.Write(Right is not null);
        Right?.WriteTo(writer);
    }

    /// <summary>
    /// Reads an immutable node and its subtree from a binary reader.
    /// </summary>
    internal static ImmutableBallTreeNode? ReadFrom(BinaryReader reader, VectorList vectors, Span<byte> guidBuffer)
    {
        var centerId = reader.ReadGuid(guidBuffer);
        var center = vectors.GetById(centerId);
        if (center is null)
        {
            return null;
        }

        var radius = reader.ReadDouble();
        var left = reader.ReadBoolean() ? ReadFrom(reader, vectors, guidBuffer) : null;
        var right = reader.ReadBoolean() ? ReadFrom(reader, vectors, guidBuffer) : null;

        return new ImmutableBallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = left,
            Right = right
        };
    }

    /// <summary>
    /// Reads an immutable node and its subtree from a binary reader.
    /// Uses both external vectors and internal vectors (computed centers) for lookup.
    /// </summary>
    internal static ImmutableBallTreeNode? ReadFrom(BinaryReader reader, VectorList vectors, VectorList internalVectors, byte[] guidBuffer)
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

        return new ImmutableBallTreeNode
        {
            Center = center,
            Radius = radius,
            Left = left,
            Right = right
        };
    }
}
