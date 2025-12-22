namespace Neighborly.Search;

/// <summary>
/// Immutable KD-tree node supporting structural sharing and lock-free reads.
/// Uses record semantics for value-based equality and immutability.
/// </summary>
public sealed record ImmutableKDTreeNode
{
    /// <summary>
    /// The vector stored at this node.
    /// </summary>
    public required Vector Vector { get; init; }

    /// <summary>
    /// The left child node (vectors with smaller values on the split axis).
    /// </summary>
    public ImmutableKDTreeNode? Left { get; init; }

    /// <summary>
    /// The right child node (vectors with larger values on the split axis).
    /// </summary>
    public ImmutableKDTreeNode? Right { get; init; }

    /// <summary>
    /// Returns true if this node has no children.
    /// </summary>
    public bool IsLeaf => Left is null && Right is null;

    /// <summary>
    /// Writes this node and its subtree to a binary writer.
    /// </summary>
    internal void WriteTo(BinaryWriter writer)
    {
        writer.Write(Vector.Id.ToByteArray());
        writer.Write(Left is not null);
        Left?.WriteTo(writer);
        writer.Write(Right is not null);
        Right?.WriteTo(writer);
    }

    /// <summary>
    /// Reads an immutable node and its subtree from a binary reader.
    /// </summary>
    internal static ImmutableKDTreeNode? ReadFrom(BinaryReader reader, VectorList vectors, Span<byte> guidBuffer)
    {
        var vectorId = reader.ReadGuid(guidBuffer);
        var vector = vectors.GetById(vectorId);
        if (vector is null)
        {
            return null;
        }

        var left = reader.ReadBoolean() ? ReadFrom(reader, vectors, guidBuffer) : null;
        var right = reader.ReadBoolean() ? ReadFrom(reader, vectors, guidBuffer) : null;

        return new ImmutableKDTreeNode
        {
            Vector = vector,
            Left = left,
            Right = right
        };
    }
}
