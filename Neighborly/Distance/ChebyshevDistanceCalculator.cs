namespace Neighborly.Distance;

/// <summary>
/// Calculates distance metric using Chebyshev distance
/// </summary>
public sealed class ChebyshevDistanceCalculator : AbstractBatchDistanceCalculator
{
    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float max = 0;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            float diff = Math.Abs(vector1.Values[i] - vector2.Values[i]);
            if (diff > max)
            {
                max = diff;
            }
        }

        return max;
    }
}
