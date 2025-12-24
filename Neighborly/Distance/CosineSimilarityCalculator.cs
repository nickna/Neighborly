namespace Neighborly.Distance;

/// <summary>
/// Calculates distance metric using Cosine similarity
/// </summary>
public sealed class CosineSimilarityCalculator : AbstractBatchDistanceCalculator
{
    protected override unsafe float CalculateDistanceCore(Vector vector1, Vector vector2)
    {
        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;
        var dimension = vector1.Dimension;

        fixed (float* p1 = vector1.Values, p2 = vector2.Values)
        {
            for (var i = 0; i < dimension; i++)
            {
                dotProduct += p1[i] * p2[i];
                magnitudeA += p1[i] * p1[i];
                magnitudeB += p2[i] * p2[i];
            }
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
