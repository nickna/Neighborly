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

    protected override unsafe float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float sum = 0;
        var dimension = vector1.Dimension;

        fixed (float* p1 = vector1.Values, p2 = vector2.Values)
        {
            for (var i = 0; i < dimension; i++)
            {
                var diff = p1[i] - p2[i];
                sum += diff * diff;
            }
        }

        return MathF.Sqrt(sum);
    }
}
