namespace Neighborly.Search;

public class KDTreeNode
{
    public required Vector Vector { get; set; }
    public KDTreeNode? Left { get; set; }
    public KDTreeNode? Right { get; set; }

    internal int Count() => 1 + (Left?.Count() ?? 0) + (Right?.Count() ?? 0);

    internal void WriteTo(BinaryWriter writer)
    {
        writer.Write(Vector.Id.ToByteArray());
        Left?.WriteTo(writer);
        Right?.WriteTo(writer);
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
}
