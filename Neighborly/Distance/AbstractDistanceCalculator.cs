namespace Neighborly.Distance;

public abstract class AbstractDistanceCalculator : IDistanceCalculator
{
    /// <inheritdoc />
    public float CalculateDistance(Vector vector1, Vector vector2)
    {
        ArgumentNullException.ThrowIfNull(vector1);
        ArgumentNullException.ThrowIfNull(vector2);
        Vector.GuardDimensionsMatch(vector1, vector2);

        return CalculateDistanceCore(vector1, vector2);
    }

    protected abstract float CalculateDistanceCore(Vector vector1, Vector vector2);
}
