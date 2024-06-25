using Neighborly.Distance;

namespace Neighborly.Tests.Helpers;

public class MockDistanceCalculator : AbstractDistanceCalculator
{
    private readonly Vector _vector1;

    private readonly Vector _vector2;

    private readonly float _distance;

    public MockDistanceCalculator(Vector vector1, Vector vector2, float distance)
    {
        ArgumentNullException.ThrowIfNull(vector1);
        ArgumentNullException.ThrowIfNull(vector2);

        _vector1 = vector1;
        _vector2 = vector2;
        _distance = distance;
    }

    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        if (ReferenceEquals(vector1, _vector1) && ReferenceEquals(vector2, _vector2))
        {
            return _distance;
        }

        return float.PositiveInfinity;
    }
}
