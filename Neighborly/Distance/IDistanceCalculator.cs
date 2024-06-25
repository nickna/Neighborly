namespace Neighborly.Distance;

/// <summary>
/// Represents a distance calculator that calculates the distance between two vectors.
/// </summary>
public interface IDistanceCalculator
{
    /// <summary>
    /// Calculates the distance between two vectors.
    /// </summary>
    /// <param name="vector1">The first vector.</param>
    /// <param name="vector2">The second vector.</param>
    /// <returns>The distance between the two vectors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="vector1"/> or <paramref name="vector2"/> is <see langword="null"/>.</exception>
    float CalculateDistance(Vector vector1, Vector vector2);
}
