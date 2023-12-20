using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tracking.Services.LoggingExtensions;

internal static partial class TrackingCacheLoggingExtensions
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Loading cache from {fileName}")]
    public static partial void LoadingCache(this ILogger<TrackingCache> logger, string fileName);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Saving cache to {fileName}")]
    public static partial void SavingCache(this ILogger<TrackingCache> logger, string fileName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Loaded {packageId} {version} from cache")]
    public static partial void LoadedPackageVersionFromCache(this ILogger<TrackingCache> logger, string packageId, string version);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Updated cache of {packageId} from {existing} to {version}")]
    public static partial void UpdatingPackageVersion(this ILogger<TrackingCache> logger, string packageId, string existing, string version);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Adding cache of {packageId} at {version}")]
    public static partial void AddingPackageToCache(this ILogger<TrackingCache> logger, string packageId, string version);
}