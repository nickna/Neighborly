using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neighborly
{
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.InMemory;

    public static class Logging
    {
        public static Serilog.ILogger Logger { get; private set; }
        private const string LogFileName = "logs/neighborly.txt";

        /// <summary>
        ///  Static constructor to initialize the logger.
        ///  On mobile, this only logs fatal messages.
        /// </summary>
        static Logging()
        {
            if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            {
                Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(LogFileName, rollingInterval: RollingInterval.Day)
                    .CreateLogger();
            }
            else // only log fatal messages on mobile
            {
                Logger = new LoggerConfiguration()
                    .WriteTo.File(LogFileName, rollingInterval: RollingInterval.Day)
                    .MinimumLevel.Fatal()
                    .CreateLogger();
            }
        }

        /// <summary>
        /// Set up the logger with the specified settings.
        /// </summary>
        /// <param name="useInMemorySink"></param>
        /// <param name="level"></param>
        public static void Initialize(bool useInMemorySink = false, LogEventLevel level = LogEventLevel.Warning)
        {
            // Locked because this Initialize() can be called while the logger is being used.
            lock (Logger) 
            {
                if (useInMemorySink)
                {
                    Logger = new LoggerConfiguration()
                        .WriteTo.Sink(new InMemorySink())
                        .MinimumLevel.Is(level)
                        .CreateLogger();
                }
                else
                {
                    Logger = new LoggerConfiguration()
                        .WriteTo.Console()
                        .WriteTo.File(LogFileName, rollingInterval: RollingInterval.Day)
                        .MinimumLevel.Is(level)
                        .CreateLogger();
                }
            }
        }

        public static ILoggerFactory LoggerFactory
        {
            get
            {
                return new LoggerFactory().AddSerilog(Logger);
            }
        }

    }

}
