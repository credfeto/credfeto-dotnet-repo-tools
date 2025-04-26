using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;

internal static partial class BulkPackageConfigLoaderLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loading package configuration from {source}..."
    )]
    public static partial void LoadingPackageConfig(this ILogger<BulkPackageConfigLoader> logger, string source);
}
