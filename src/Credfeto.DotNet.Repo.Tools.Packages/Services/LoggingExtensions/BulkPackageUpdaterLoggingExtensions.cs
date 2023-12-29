using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;

internal static partial class BulkPackageUpdaterLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Solution check failed")]
    public static partial void LogSolutionCheckFailed(this ILogger<BulkPackageUpdater> logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Build failed (On repo check)")]
    public static partial void LogBuildFailedOnRepoCheck(this ILogger<BulkPackageUpdater> logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Release created - aborting run")]
    public static partial void LogReleaseCreatedAbortingRun(this ILogger<BulkPackageUpdater> logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[CACHE] Pre-loading cached packages")]
    public static partial void LogPreLoadingCachedPackages(this ILogger<BulkPackageUpdater> logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[CACHE] Updating {packageId}")]
    public static partial void LogUpdatingCachedPackage(this ILogger<BulkPackageUpdater> logger, string packageId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "[CACHE] Update {packageId} updated {count} packages")]
    public static partial void LogUpdatedCachedPackages(this ILogger<BulkPackageUpdater> logger, string packageId, int count);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "[CACHE] Total package updates: {count}")]
    public static partial void LogUpdatedCachedPackagesTotal(this ILogger<BulkPackageUpdater> logger, int count);
}