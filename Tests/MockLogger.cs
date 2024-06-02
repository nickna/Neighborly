using Microsoft.Extensions.Logging;

internal class MockLogger<TCategoryName> : ILogger<TCategoryName>
{
    public LogLevel? LastLogLevel { get; private set; }
    public EventId? LastEventId { get; private set; }
    public object? LastState { get; private set; }
    public Exception? LastException { get; private set; }
    public string? LastMessage { get; private set; }

    public IDisposable? BeginScope<TState>(TState? state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LastLogLevel = logLevel;
        LastEventId = eventId;
        LastState = state;
        LastException = exception;
        LastMessage = formatter(state, exception);
    }
}