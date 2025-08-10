using Credfeto.Package;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;

internal static partial class PackageUpdateConfigurationBuilderLoggingExtensions
{
    [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "Including {packageId} (Using Prefix match: {prefix})")]
    private static partial void LogIncludingPackage(this ILogger<PackageUpdateConfigurationBuilder> logger, string packageId, bool prefix);

    public static void LogIncludingPackage(this ILogger<PackageUpdateConfigurationBuilder> logger, PackageMatch package)
    {
        logger.LogIncludingPackage(packageId: package.PackageId, prefix: package.Prefix);
    }

    [LoggerMessage(EventId = 17, Level = LogLevel.Information, Message = "Excluding {packageId} (Using Prefix match: {prefix})")]
    private static partial void LogExcludingPackage(this ILogger<PackageUpdateConfigurationBuilder> logger, string packageId, bool prefix);

    public static void LogExcludingPackage(this ILogger<PackageUpdateConfigurationBuilder> logger, PackageMatch package)
    {
        logger.LogExcludingPackage(packageId: package.PackageId, prefix: package.Prefix);
    }
}