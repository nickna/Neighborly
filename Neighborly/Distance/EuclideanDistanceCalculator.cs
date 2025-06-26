namespace Neighborly.Distance;

/// <summary>
/// Calculates distance using Euclidean math
/// </summary>
public sealed class EuclideanDistanceCalculator : AbstractBatchDistanceCalculator
{
    /// <summary>
    /// Static instance of the <see cref="EuclideanDistanceCalculator"/>, which can be used directly
    /// as a default for distance calculations. This is, so that we don't have to create a new instance
    /// for every distance calculation, which would be wasteful.
    /// </summary>
    internal static EuclideanDistanceCalculator Instance { get; } = new EuclideanDistanceCalculator();

    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float sum = 0;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            float diff = vector1.Values[i] - vector2.Values[i];
            sum += diff * diff;
        }

        return MathF.Sqrt(sum);
    }
}
