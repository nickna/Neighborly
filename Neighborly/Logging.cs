using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace Neighborly;

public static class Logging
{
    private static readonly Lock _lock = new();
    private const string LogFileName = "logs/neighborly.txt";

    private static Serilog.ILogger _logger = null!;
    private static ILoggerFactory? _loggerFactory;

    public static Serilog.ILogger Logger => _logger;

    /// <summary>
    /// Static constructor to initialize the logger.
    /// On mobile, this only logs fatal messages.
    /// </summary>
    static Logging()
    {
        // Ensure log directory exists
        var logDirectory = Path.GetDirectoryName(LogFileName);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
        {
            _logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(LogFileName, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
        else // only log fatal messages on mobile
        {
            _logger = new LoggerConfiguration()
                .WriteTo.File(LogFileName, rollingInterval: RollingInterval.Day)
                .MinimumLevel.Fatal()
                .CreateLogger();
        }
    }

    /// <summary>
    /// Set up the logger with the specified settings.
    /// </summary>
    /// <param name="useInMemorySink">If true, logs to an in-memory sink instead of console/file.</param>
    /// <param name="level">The minimum log event level.</param>
    public static void Initialize(bool useInMemorySink = false, LogEventLevel level = LogEventLevel.Warning)
    {
        using (_lock.EnterScope())
        {
            // Dispose the old logger before creating a new one
            (_logger as IDisposable)?.Dispose();

            // Invalidate the cached logger factory
            _loggerFactory?.Dispose();
            _loggerFactory = null;

            if (useInMemorySink)
            {
                _logger = new LoggerConfiguration()
                    .WriteTo.Sink(new InMemorySink())
                    .MinimumLevel.Is(level)
                    .CreateLogger();
            }
            else
            {
                _logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(LogFileName, rollingInterval: RollingInterval.Day)
                    .MinimumLevel.Is(level)
                    .CreateLogger();
            }
        }
    }

    /// <summary>
    /// Gets the logger factory, creating one if necessary.
    /// The factory is invalidated when Initialize() is called.
    /// </summary>
    public static ILoggerFactory LoggerFactory
    {
        get
        {
            using (_lock.EnterScope())
            {
                _loggerFactory ??= new LoggerFactory().AddSerilog(_logger);
                return _loggerFactory;
            }
        }
    }

    /// <summary>
    /// Shuts down the logging system, flushing any buffered log events.
    /// Should be called before application exit.
    /// </summary>
    public static void Shutdown()
    {
        using (_lock.EnterScope())
        {
            _loggerFactory?.Dispose();
            _loggerFactory = null;

            (_logger as IDisposable)?.Dispose();
        }
    }
}
