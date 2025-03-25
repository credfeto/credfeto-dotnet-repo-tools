using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Release.Services.LoggingExtensions;

internal static partial class ReleaseConfigLoaderLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loading release configuration from {source}..."
    )]
    public static partial void LoadingReleaseConfig(
        this ILogger<ReleaseConfigLoader> logger,
        string source
    );
}
