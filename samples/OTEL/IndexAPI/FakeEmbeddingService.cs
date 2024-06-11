using System.Diagnostics;

namespace IndexAPI;

public class FakeEmbeddingService(Instrumentation instrumentation)
{
    private readonly Instrumentation _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));

    public async ValueTask<float[]> GetEmbeddingsAsync(string text, CancellationToken cancellationToken = default)
    {
        using var activity = _instrumentation.ActivitySource.StartActivity(nameof(GetEmbeddingsAsync));

        try
        {
            return await GetEmbeddingsImplAsync(text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.ToString());
            throw;
        }
        finally
        {
            activity?.Stop();
        }
    }

    public async ValueTask<float[]> GetEmbeddingsImplAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(Random.Shared.Next(4_2, 1_3_3_7), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return Enumerable.Range(0, text.Length).Select(static _ => (float)Random.Shared.NextDouble()).ToArray();
    }
}
