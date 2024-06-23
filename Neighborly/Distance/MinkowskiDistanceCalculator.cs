namespace Neighborly.Distance;

/// <summary>
/// Calculates distance metric using Minkowski distance
/// </summary>
public sealed class MinkowskiDistanceCalculator : AbstractDistanceCalculator
{
    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float sum = 0;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            float diff = vector1.Values[i] - vector2.Values[i];
            sum += MathF.Pow(Math.Abs(diff), 3);
        }

        return MathF.Pow(sum, 1.0f / 3.0f);
    }
}
