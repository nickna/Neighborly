using System.Collections.Immutable;
using Neighborly.Persistence.VersionHandlers;

namespace Neighborly.Persistence;

/// <summary>
/// Registry for version handlers with efficient dispatch.
/// Provides centralized management of supported file format versions.
/// </summary>
internal static class VersionHandlerRegistry
{
    private static readonly ImmutableDictionary<int, IVersionHandler> s_handlers =
        new Dictionary<int, IVersionHandler>
        {
            [0] = new V0Handler(),
            [1] = new V1Handler()
        }.ToImmutableDictionary();

    /// <summary>
    /// Gets the current file format version that will be written.
    /// </summary>
    public static int CurrentVersion => 1;

    /// <summary>
    /// Gets the version handler for the specified version number.
    /// </summary>
    /// <param name="version">The file format version number.</param>
    /// <returns>The version handler for the specified version.</returns>
    /// <exception cref="NotSupportedException">Thrown when the version is not supported.</exception>
    public static IVersionHandler GetHandler(int version)
    {
        if (!s_handlers.TryGetValue(version, out var handler))
        {
            var supportedVersions = string.Join(", ", s_handlers.Keys.Order());
            throw new NotSupportedException(
                $"File version {version} is not supported. Supported versions: {supportedVersions}");
        }
        return handler;
    }

    /// <summary>
    /// Gets the handler for the current version.
    /// </summary>
    /// <returns>The current version handler.</returns>
    public static IVersionHandler GetCurrentHandler() => s_handlers[CurrentVersion];

    /// <summary>
    /// Determines whether the specified version is supported.
    /// </summary>
    /// <param name="version">The version number to check.</param>
    /// <returns>True if the version is supported; otherwise, false.</returns>
    public static bool IsSupported(int version) => s_handlers.ContainsKey(version);
}
