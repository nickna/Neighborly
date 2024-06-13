using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Neighborly;

public class Instrumentation : IDisposable
{
    internal const string ActivitySourceName = nameof(Neighborly);

    internal const string MeterName = nameof(Neighborly);

    private bool _disposedValue;

    public Instrumentation()
    {
        string? version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
        ActivitySource = new(ActivitySourceName, version);
        Meter = new(MeterName, version);
    }

    public Instrumentation(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        string? version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
        ActivitySource = new(ActivitySourceName, version);
        Meter = meterFactory.Create(MeterName, version, [new("db.system", "neighborly")]);
    }

    public static Instrumentation Instance { get; } = new Instrumentation();

    public ActivitySource ActivitySource { get; }

    public Meter Meter { get; }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                ActivitySource.Dispose();
                Meter.Dispose();
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
