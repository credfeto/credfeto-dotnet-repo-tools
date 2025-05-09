using System;
using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;

internal static partial class BulkCodeCleanUpLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Solution check failed")]
    public static partial void LogSolutionCheckFailed(this ILogger<BulkCodeCleanUp> logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Build failed (On repo check)")]
    public static partial void LogBuildFailedOnRepoCheck(this ILogger<BulkCodeCleanUp> logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Release created - aborting run")]
    public static partial void LogReleaseCreatedAbortingRun(this ILogger<BulkCodeCleanUp> logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[CACHE] Pre-loading cached packages")]
    public static partial void LogPreLoadingCachedPackages(this ILogger<BulkCodeCleanUp> logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[CACHE] Updating {packageId}")]
    public static partial void LogUpdatingCachedPackage(this ILogger<BulkCodeCleanUp> logger, string packageId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "[CACHE] Update {packageId} updated {count} packages"
    )]
    public static partial void LogUpdatedCachedPackages(
        this ILogger<BulkCodeCleanUp> logger,
        string packageId,
        int count
    );

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "[CACHE] Total package updates: {count}")]
    public static partial void LogUpdatedCachedPackagesTotal(this ILogger<BulkCodeCleanUp> logger, int count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Processing {repo}")]
    public static partial void LogProcessingRepo(this ILogger<BulkCodeCleanUp> logger, string repo);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "No CHANGELOG.md found")]
    public static partial void LogNoChangelogFound(this ILogger<BulkCodeCleanUp> logger);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "No DotNet files found")]
    public static partial void LogNoDotNetFilesFound(this ILogger<BulkCodeCleanUp> logger);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Resetting {clonePath} to {branch}")]
    private static partial void LogResettingToDefault(
        this ILogger<BulkCodeCleanUp> logger,
        string clonePath,
        string branch
    );

    public static void LogResettingToDefault(this ILogger<BulkCodeCleanUp> logger, in RepoContext repoContext)
    {
        logger.LogResettingToDefault(clonePath: repoContext.ClonePath, branch: repoContext.DefaultBranch);
    }

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "{clonePath}: Committing {packageId} ({version}) to {branch}"
    )]
    private static partial void LogCommittingPackageToBranch(
        this ILogger<BulkCodeCleanUp> logger,
        string clonePath,
        string packageId,
        string version,
        string branch
    );

    public static void LogCommittingToDefault(
        this ILogger<BulkCodeCleanUp> logger,
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
        this ILogger<BulkCodeCleanUp> logger,
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
        this ILogger<BulkCodeCleanUp> logger,
        string clonePath,
        string packageId,
        string version,
        string branch
    );

    public static void LogSkippingPackageCommit(
        this ILogger<BulkCodeCleanUp> logger,
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

    [LoggerMessage(EventId = 18, Level = LogLevel.Warning, Message = "{message}")]
    public static partial void LogReleaseCreated(
        this ILogger<BulkCodeCleanUp> logger,
        string message,
        Exception exception
    );

    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Warning,
        Message = "SDK {sdkVersion} was requested, but not installed.  Currently installed SDKS: {installedSdks}"
    )]
    private static partial void LogMissingSdk(
        this ILogger<BulkCodeCleanUp> logger,
        string sdkVersion,
        string installedSdks
    );

    public static void LogMissingSdk(
        this ILogger<BulkCodeCleanUp> logger,
        Version sdkVersion,
        IReadOnlyList<Version> installedSdks
    )
    {
        logger.LogMissingSdk(sdkVersion.ToString(), string.Join(separator: ", ", values: installedSdks));
    }

    [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "Project Cleanup Starting: {filename}")]
    public static partial void StartingProjectCleaup(this ILogger<BulkCodeCleanUp> logger, string filename);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Information,
        Message = "Project Cleanup Completed: {filename} Changes: {changes}"
    )]
    public static partial void CompletingProjectCleanup(
        this ILogger<BulkCodeCleanUp> logger,
        string filename,
        int changes
    );

    [LoggerMessage(
        EventId = 22,
        Level = LogLevel.Information,
        Message = "Project Cleanup Failed: {filename} Status: {message}"
    )]
    public static partial void FailedProjectCleanup(
        this ILogger<BulkCodeCleanUp> logger,
        string filename,
        string message,
        Exception exception
    );

    [LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "Cleaning: {filename}")]
    public static partial void CleaningFile(this ILogger<BulkCodeCleanUp> logger, string filename);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information, Message = "Cleaning: {filename} (unchanged)")]
    public static partial void CleaningFileUnchanged(this ILogger<BulkCodeCleanUp> logger, string filename);

    [LoggerMessage(EventId = 25, Level = LogLevel.Information, Message = "Cleaning: {filename} (introduced changes)")]
    public static partial void CleaningFileDifferent(this ILogger<BulkCodeCleanUp> logger, string filename);
}
