using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tracking.Services.LoggingExtensions;

internal static partial class TrackingCacheLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "[TRACKING] Loading repository tracking from {fileName}")]
    public static partial void LoadingCache(this ILogger<TrackingCache> logger, string fileName);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[TRACKING] Saving repository tracking to {fileName}")]
    public static partial void SavingCache(this ILogger<TrackingCache> logger, string fileName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "[TRACKING] Loaded {repository} SHA {version} from cache")]
    public static partial void LoadedPackageVersionFromCache(this ILogger<TrackingCache> logger, string repository, string version);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "[TRACKING] Updated {repository} from SHA {existing} to SHA {version}")]
    public static partial void UpdatingPackageVersion(this ILogger<TrackingCache> logger, string repository, string existing, string version);
}