namespace Neighborly.API
{
    /// <summary>
    /// Provides extension methods for logging.
    /// </summary>
    internal static partial class LoggerExtensions
    {
        /// <summary>
        /// Logs a critical message when both gRPC and REST protocols are disabled and the service is shutting down.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <remarks>
        /// This method is used to log a critical message when both gRPC and REST protocols are disabled and the service is shutting down.
        /// </remarks>
        [LoggerMessage(LogLevel.Critical, "Both gRPC and REST are disabled. Service is shutting down.")]
        public static partial void AppShuttingDownAsNoProtocolsEnabled(this ILogger logger);
    }
}
