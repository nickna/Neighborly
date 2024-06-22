namespace Neighborly.Search;

public class BallTreeNode 
{
    public required Vector Center { get; set; }
    public double Radius { get; set; }
    public BallTreeNode? Left { get; set; }
    public BallTreeNode? Right { get; set; }
}
