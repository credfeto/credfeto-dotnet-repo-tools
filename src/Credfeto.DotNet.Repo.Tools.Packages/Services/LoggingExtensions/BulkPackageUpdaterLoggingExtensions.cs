using System;
using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.Package;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;

internal static partial class BulkPackageUpdaterLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Solution check failed")]
    public static partial void LogSolutionCheckFailed(this ILogger<BulkPackageUpdater> logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Build failed (On repo check)")]
    public static partial void LogBuildFailedOnRepoCheck(this ILogger<BulkPackageUpdater> logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Release created - aborting run")]
    public static partial void LogReleaseCreatedAbortingRun(
        this ILogger<BulkPackageUpdater> logger,
        Exception exception
    );

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[CACHE] Pre-loading cached packages")]
    public static partial void LogPreLoadingCachedPackages(this ILogger<BulkPackageUpdater> logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[CACHE] Updating {packageId}")]
    public static partial void LogUpdatingCachedPackage(this ILogger<BulkPackageUpdater> logger, string packageId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "[CACHE] Update {packageId} updated {count} packages"
    )]
    public static partial void LogUpdatedCachedPackages(
        this ILogger<BulkPackageUpdater> logger,
        string packageId,
        int count
    );

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "[CACHE] Total package updates: {count}")]
    public static partial void LogUpdatedCachedPackagesTotal(this ILogger<BulkPackageUpdater> logger, int count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Processing {repo}")]
    public static partial void LogProcessingRepo(this ILogger<BulkPackageUpdater> logger, string repo);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "No CHANGELOG.md found")]
    public static partial void LogNoChangelogFound(this ILogger<BulkPackageUpdater> logger);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "No DotNet files found")]
    public static partial void LogNoDotNetFilesFound(this ILogger<BulkPackageUpdater> logger);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Resetting {clonePath} to {branch}")]
    private static partial void LogResettingToDefault(
        this ILogger<BulkPackageUpdater> logger,
        string clonePath,
        string branch
    );

    public static void LogResettingToDefault(this ILogger<BulkPackageUpdater> logger, in RepoContext repoContext)
    {
        logger.LogResettingToDefault(clonePath: repoContext.ClonePath, branch: repoContext.DefaultBranch);
    }

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "{clonePath}: Committing {packageId} ({version}) to {branch}"
    )]
    private static partial void LogCommittingPackageToBranch(
        this ILogger<BulkPackageUpdater> logger,
        string clonePath,
        string packageId,
        string version,
        string branch
    );

    public static void LogCommittingToDefault(
        this ILogger<BulkPackageUpdater> logger,
        in RepoContext repoContext,
        string packageId,
        string version
    )
    {
        logger.LogCommittingPackageToBranch(
            clonePath: repoContext.ClonePath,
            packageId: packageId,
            version: version,
            branch: repoContext.DefaultBranch
        );
    }

    public static void LogCommittingToNamedBranch(
        this ILogger<BulkPackageUpdater> logger,
        in RepoContext repoContext,
        string branch,
        string packageId,
        string version
    )
    {
        logger.LogCommittingPackageToBranch(
            clonePath: repoContext.ClonePath,
            packageId: packageId,
            version: version,
            branch: branch
        );
    }

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "{clonePath}: Skipping commit of {packageId} {version} as branch {branch} already exists"
    )]
    private static partial void LogSkippingPackageCommit(
        this ILogger<BulkPackageUpdater> logger,
        string clonePath,
        string packageId,
        string version,
        string branch
    );

    public static void LogSkippingPackageCommit(
        this ILogger<BulkPackageUpdater> logger,
        in RepoContext repoContext,
        string branch,
        string packageId,
        string version
    )
    {
        logger.LogSkippingPackageCommit(
            clonePath: repoContext.ClonePath,
            packageId: packageId,
            version: version,
            branch: branch
        );
    }

    [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "Build failed (after updating package)")]
    public static partial void LogBuildFailedAfterPackageUpdate(
        this ILogger<BulkPackageUpdater> logger,
        Exception exception
    );

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "* Updating {packageId}...")]
    public static partial void LogUpdatingPackageId(this ILogger<BulkPackageUpdater> logger, string packageId);

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Information,
        Message = "Including {packageId} (Using Prefix match: {prefix})"
    )]
    private static partial void LogIncludingPackage(
        this ILogger<BulkPackageUpdater> logger,
        string packageId,
        bool prefix
    );

    public static void LogIncludingPackage(this ILogger<BulkPackageUpdater> logger, PackageMatch package)
    {
        logger.LogIncludingPackage(packageId: package.PackageId, prefix: package.Prefix);
    }

    [LoggerMessage(
        EventId = 17,
        Level = LogLevel.Information,
        Message = "Excluding {packageId} (Using Prefix match: {prefix})"
    )]
    private static partial void LogExcludingPackage(
        this ILogger<BulkPackageUpdater> logger,
        string packageId,
        bool prefix
    );

    public static void LogExcludingPackage(this ILogger<BulkPackageUpdater> logger, PackageMatch package)
    {
        logger.LogExcludingPackage(packageId: package.PackageId, prefix: package.Prefix);
    }

    [LoggerMessage(EventId = 18, Level = LogLevel.Warning, Message = "{message}")]
    public static partial void LogReleaseCreated(
        this ILogger<BulkPackageUpdater> logger,
        string message,
        Exception exception
    );

    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Warning,
        Message = "SDK {sdkVersion} was requested, but not installed.  Currently installed SDKS: {installedSdks}"
    )]
    private static partial void LogMissingSdk(
        this ILogger<BulkPackageUpdater> logger,
        string sdkVersion,
        string installedSdks
    );

    public static void LogMissingSdk(
        this ILogger<BulkPackageUpdater> logger,
        Version sdkVersion,
        IReadOnlyList<Version> installedSdks
    )
    {
        logger.LogMissingSdk(sdkVersion.ToString(), string.Join(separator: ", ", values: installedSdks));
    }

    [LoggerMessage(EventId = 20, Level = LogLevel.Error, Message = "Could not create release: {message}")]
    public static partial void LogBuildFailedOnCreateRelease(
        this ILogger<BulkPackageUpdater> logger,
        string message,
        Exception exception
    );
}
