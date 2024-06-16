namespace Neighborly.Search;

public class BallTreeNode
{
    public required Vector Center { get; set; }
    public double Radius { get; set; }
    public BallTreeNode? Left { get; set; }
    public BallTreeNode? Right { get; set; }

    internal int Count() => 1 + (Left?.Count() ?? 0) + (Right?.Count() ?? 0);

    internal void WriteTo(BinaryWriter writer, bool treeIdOnly = false)
    {
        writer.Write(Center.Id.ToByteArray());
        if (!treeIdOnly)
        {
            writer.Write(Radius);
            Left?.WriteTo(writer, true);
            Right?.WriteTo(writer, true);
        }
    }
}
