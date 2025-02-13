using Credfeto.DotNet.Repo.Tools.Models;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.Release.Services.LoggingExtensions;

internal static partial class ReleaseGenerationLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "{clonePath}: RELEASE CREATED: {version}"
    )]
    private static partial void LogReleaseCreated(
        this ILogger<ReleaseGeneration> logger,
        string clonePath,
        string version
    );

    public static void LogReleaseCreated(
        this ILogger<ReleaseGeneration> logger,
        in RepoContext repoContext,
        string version
    )
    {
        logger.LogReleaseCreated(clonePath: repoContext.ClonePath, version: version);
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Change log update score: {autoUpdateCount}"
    )]
    public static partial void LogChangeLogUpdateScore(
        this ILogger<ReleaseGeneration> logger,
        int autoUpdateCount
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "{clonePath}: RELEASE SKIPPED: {skippingReason}"
    )]
    private static partial void LogReleaseSkipped(
        this ILogger<ReleaseGeneration> logger,
        string clonePath,
        string skippingReason
    );

    public static void LogReleaseSkipped(
        this ILogger<ReleaseGeneration> logger,
        in RepoContext repoContext,
        ReleaseSkippingReason skippingReason
    )
    {
        logger.LogReleaseSkipped(clonePath: repoContext.ClonePath, skippingReason.GetDescription());
    }

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "{clonePath}: Found Matching Update: {packageId} for score {score}"
    )]
    private static partial void LogMatchedPackage(
        this ILogger<ReleaseGeneration> logger,
        string clonePath,
        string packageId,
        int score
    );

    public static void LogMatchedPackage(
        this ILogger<ReleaseGeneration> logger,
        in RepoContext repoContext,
        string packageId,
        int score
    )
    {
        logger.LogMatchedPackage(
            clonePath: repoContext.ClonePath,
            packageId: packageId,
            score: score
        );
    }

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "{clonePath}: Skipping Ignored Update: {packageId} for score {score}"
    )]
    private static partial void LogIgnoredPackage(
        this ILogger<ReleaseGeneration> logger,
        string clonePath,
        string packageId,
        int score
    );

    public static void LogIgnoredPackage(
        this ILogger<ReleaseGeneration> logger,
        in RepoContext repoContext,
        string packageId,
        int score
    )
    {
        logger.LogIgnoredPackage(
            clonePath: repoContext.ClonePath,
            packageId: packageId,
            score: score
        );
    }

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Last release was: {version}"
    )]
    private static partial void LogLastRelease(
        this ILogger<ReleaseGeneration> logger,
        string version
    );

    public static void LogLastRelease(this ILogger<ReleaseGeneration> logger, NuGetVersion version)
    {
        logger.LogLastRelease(version.ToString());
    }

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Next release is: {version}"
    )]
    private static partial void LogNextRelease(
        this ILogger<ReleaseGeneration> logger,
        string version
    );

    public static void LogNextRelease(this ILogger<ReleaseGeneration> logger, NuGetVersion version)
    {
        logger.LogNextRelease(version.ToString());
    }
}
