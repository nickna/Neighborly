using System.Diagnostics;

namespace IndexAPI;

public class Instrumentation : IDisposable
{
    internal const string ActivitySourceName = $"{nameof(Neighborly)}.{nameof(IndexAPI)}";

    private bool _disposedValue;

    public Instrumentation()
    {
        string? version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
        ActivitySource = new(ActivitySourceName, version);
    }

    public ActivitySource ActivitySource { get; }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                ActivitySource.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}