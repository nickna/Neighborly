namespace Neighborly.Distance;

/// <summary>
/// Calculates distance metric using Cosine similarity
/// </summary>
public sealed class CosineSimilarityCalculator : AbstractBatchDistanceCalculator
{
    protected override float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;
        for (int i = 0; i < vector1.Dimension; i++)
        {
            dotProduct += vector1.Values[i] * vector2.Values[i];
            magnitudeA += vector1.Values[i] * vector1.Values[i];
            magnitudeB += vector2.Values[i] * vector2.Values[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);
        return dotProduct / (magnitudeA * magnitudeB);
    }
}
