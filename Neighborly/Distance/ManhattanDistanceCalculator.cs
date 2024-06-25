namespace Neighborly.Distance;

/// <summary>
/// Calculate distance metric using Manhattan distance
/// </summary>
public sealed class ManhattanDistanceCalculator : AbstractDistanceCalculator
{
    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float sum = 0;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            float diff = vector1.Values[i] - vector2.Values[i];
            sum += Math.Abs(diff);
        }

        return sum;
    }
}
