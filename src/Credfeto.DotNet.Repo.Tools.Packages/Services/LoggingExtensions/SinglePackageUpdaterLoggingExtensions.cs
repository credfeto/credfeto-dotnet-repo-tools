using System;
using Credfeto.DotNet.Repo.Tools.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;

internal static partial class SinglePackageUpdaterLoggingExtensions
{
    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "* Updating {packageId}...")]
    public static partial void LogUpdatingPackageId(this ILogger<SinglePackageUpdater> logger, string packageId);

    [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "Build failed (after updating package)")]
    public static partial void LogBuildFailedAfterPackageUpdate(
        this ILogger<SinglePackageUpdater> logger,
        Exception exception
    );

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Resetting {clonePath} to {branch}")]
    private static partial void LogResettingToDefault(
        this ILogger<SinglePackageUpdater> logger,
        string clonePath,
        string branch
    );

    public static void LogResettingToDefault(this ILogger<SinglePackageUpdater> logger, in RepoContext repoContext)
    {
        logger.LogResettingToDefault(clonePath: repoContext.ClonePath, branch: repoContext.DefaultBranch);
    }

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Information,
        Message = "{clonePath}: Skipping commit of {packageId} {version} as branch {branch} already exists"
    )]
    private static partial void LogSkippingPackageCommit(
        this ILogger<SinglePackageUpdater> logger,
        string clonePath,
        string packageId,
        string version,
        string branch
    );

    public static void LogSkippingPackageCommit(
        this ILogger<SinglePackageUpdater> logger,
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

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "{clonePath}: Committing {packageId} ({version}) to {branch}"
    )]
    private static partial void LogCommittingPackageToBranch(
        this ILogger<SinglePackageUpdater> logger,
        string clonePath,
        string packageId,
        string version,
        string branch
    );

    public static void LogCommittingToDefault(
        this ILogger<SinglePackageUpdater> logger,
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
        this ILogger<SinglePackageUpdater> logger,
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
}
