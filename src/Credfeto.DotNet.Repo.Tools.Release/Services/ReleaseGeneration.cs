using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.Date.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Release.Extensions;
using Credfeto.DotNet.Repo.Tools.Release.Helpers;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Release.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.BuildVersion.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.Release.Services;

public sealed class ReleaseGeneration : IReleaseGeneration
{
    private const int DEFAULT_BUILD_NUMBER = 101;

    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;

    private readonly ILogger<ReleaseGeneration> _logger;
    private readonly ICurrentTimeSource _timeSource;
    private readonly ITrackingCache _trackingCache;
    private readonly IVersionDetector _versionDetector;

    public ReleaseGeneration(
        ICurrentTimeSource timeSource,
        IVersionDetector versionDetector,
        ITrackingCache trackingCache,
        IDotNetSolutionCheck dotNetSolutionCheck,
        IDotNetBuild dotNetBuild,
        ILogger<ReleaseGeneration> logger
    )
    {
        this._timeSource = timeSource;
        this._versionDetector = versionDetector;
        this._trackingCache = trackingCache;
        this._dotNetSolutionCheck = dotNetSolutionCheck;
        this._dotNetBuild = dotNetBuild;
        this._logger = logger;
    }

    public async ValueTask TryCreateNextPatchAsync(
        RepoContext repoContext,
        string basePath,
        BuildSettings buildSettings,
        DotNetVersionSettings dotNetSettings,
        IReadOnlyList<string> solutions,
        IReadOnlyList<PackageUpdate> packages,
        ReleaseConfig releaseConfig,
        CancellationToken cancellationToken
    )
    {
        // *********************************************************
        // * 1 TEMPLATE REPOS

        if (releaseConfig.ShouldNeverAutoReleaseRepo(repoContext.ClonePath))
        {
            return;
        }

        // *********************************************************
        // * 2 RELEASE NOTES AND DURATION
        if (
            await this.ShouldNeverReleaseTimeAndContentBasedAsync(
                repoContext: repoContext,
                releaseConfig: releaseConfig,
                packages: packages,
                cancellationToken: cancellationToken
            )
        )
        {
            return;
        }

        // *********************************************************
        // * 3 CODE QUALITY AND BUILD

        if (
            await this.ShouldNeverReleaseCodeQualityAsync(
                repoContext: repoContext,
                basePath: basePath,
                buildSettings: buildSettings,
                dotNetSettings: dotNetSettings,
                solutions: solutions,
                cancellationToken: cancellationToken
            )
        )
        {
            return;
        }

        // *********************************************************
        // * 4 Dispatch

        if (
            this.ShouldNeverReleaseFuzzyRules(
                repoContext: repoContext,
                buildSettings: buildSettings,
                releaseConfig: releaseConfig
            )
        )
        {
            return;
        }

        await this.CreateAsync(repoContext: repoContext, cancellationToken: cancellationToken);
    }

    public async ValueTask CreateAsync(RepoContext repoContext, CancellationToken cancellationToken)
    {
        string nextVersion = this.GetNextVersion(repoContext: repoContext);

        await ChangeLogUpdater.CreateReleaseAsync(
            changeLogFileName: repoContext.ChangeLogFileName,
            version: nextVersion,
            pending: false,
            cancellationToken: cancellationToken
        );

        await repoContext.Repository.CommitAsync($"Changelog for {nextVersion}", cancellationToken: cancellationToken);
        await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);

        this._logger.LogReleaseCreated(repoContext: repoContext, version: nextVersion);

        this._trackingCache.Set(repoUrl: repoContext.ClonePath, value: repoContext.Repository.HeadRev);

        string releaseBranch = $"release/{nextVersion}";
        await repoContext.Repository.CreateBranchAsync(branchName: releaseBranch, cancellationToken: cancellationToken);
        await repoContext.Repository.PushOriginAsync(
            branchName: releaseBranch,
            upstream: GitConstants.Upstream,
            cancellationToken: cancellationToken
        );

        throw new ReleaseCreatedException($"Releases {nextVersion} created for {repoContext.ClonePath}");
    }

    private bool ShouldNeverReleaseFuzzyRules(
        in RepoContext repoContext,
        in BuildSettings buildSettings,
        in ReleaseConfig releaseConfig
    )
    {
        if (HasPendingDependencyUpdateBranches(repoContext))
        {
            return this.ReleaseSkipped(
                repoContext: repoContext,
                skippingReason: ReleaseSkippingReason.FOUND_PENDING_UPDATE_BRANCHES
            );
        }

        if (releaseConfig.ShouldAlwaysCreatePatchRelease(repoContext.ClonePath))
        {
            return false;
        }

        if (releaseConfig.CheckRepoForAllowedAutoUpgrade(repoContext.ClonePath))
        {
            if (!buildSettings.Publishable)
            {
                // not publishable so just upgrade
                return false;
            }

            if (releaseConfig.ShouldAlwaysReleasePublishable(repoContext.ClonePath))
            {
                return false;
            }

            this._logger.LogSkippingReleaseAsPublishableProjects(buildSettings);

            return this.ReleaseSkipped(
                repoContext: repoContext,
                skippingReason: ReleaseSkippingReason.CONTAINS_PUBLISHABLE_EXECUTABLES
            );
        }

        return this.ReleaseSkipped(
            repoContext: repoContext,
            skippingReason: ReleaseSkippingReason.EXPLICITLY_PROHIBITED
        );
    }

    private async ValueTask<bool> ShouldNeverReleaseCodeQualityAsync(
        RepoContext repoContext,
        string basePath,
        BuildSettings buildSettings,
        DotNetVersionSettings dotNetSettings,
        IReadOnlyList<string> solutions,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await this._dotNetSolutionCheck.ReleaseCheckAsync(
                solutions: solutions,
                repositoryDotNetSettings: dotNetSettings,
                cancellationToken: cancellationToken
            );
        }
        catch (SolutionCheckFailedException)
        {
            return this.ReleaseSkipped(
                repoContext: repoContext,
                skippingReason: ReleaseSkippingReason.FAILED_RELEASE_CHECK
            );
        }

        BuildOverride buildOverride = new(PreRelease: false);

        try
        {
            await this._dotNetBuild.BuildAsync(
                basePath: basePath,
                buildSettings: buildSettings,
                buildOverride: buildOverride,
                cancellationToken: cancellationToken
            );
        }
        catch (DotNetBuildErrorException)
        {
            return this.ReleaseSkipped(repoContext: repoContext, skippingReason: ReleaseSkippingReason.DOES_NOT_BUILD);
        }

        return false;
    }

    private bool ReleaseSkipped(in RepoContext repoContext, ReleaseSkippingReason skippingReason)
    {
        this._logger.LogReleaseSkipped(repoContext: repoContext, skippingReason: skippingReason);

        return true;
    }

    private async ValueTask<bool> ShouldNeverReleaseTimeAndContentBasedAsync(
        RepoContext repoContext,
        ReleaseConfig releaseConfig,
        IReadOnlyList<PackageUpdate> packages,
        CancellationToken cancellationToken
    )
    {
        string releaseNotes = await ChangeLogReader.ExtractReleaseNotesFromFileAsync(
            changeLogFileName: repoContext.ChangeLogFileName,
            version: "Unreleased",
            cancellationToken: cancellationToken
        );

        int autoUpdateCount = this.IsAllAutoUpdates(
            repoContext: repoContext,
            releaseNotes: releaseNotes,
            packages: packages
        );

        this._logger.LogChangeLogUpdateScore(autoUpdateCount);

        DateTimeOffset lastCommitDate = repoContext.Repository.GetLastCommitDate();
        DateTimeOffset now = this._timeSource.UtcNow();
        TimeSpan timeSinceLastCommit = now - lastCommitDate;

        ReleaseSkippingReason skippingReason = ReleaseSkippingReason.INSUFFICIENT_UPDATES;
        bool shouldCreateRelease = false;

        if (autoUpdateCount > releaseConfig.AutoReleasePendingPackages)
        {
            if (timeSinceLastCommit.TotalHours > releaseConfig.MinimumHoursBeforeAutoRelease)
            {
                shouldCreateRelease = true;
                skippingReason = ReleaseSkippingReason.RELEASING_NORMAL;
            }
            else
            {
                skippingReason = ReleaseSkippingReason.INSUFFICIENT_DURATION_SINCE_LAST_UPDATE;
            }
        }

        if (!shouldCreateRelease)
        {
            if (autoUpdateCount >= 1)
            {
                if (timeSinceLastCommit.TotalHours > releaseConfig.InactivityHoursBeforeAutoRelease)
                {
                    shouldCreateRelease = true;
                    skippingReason = ReleaseSkippingReason.RELEASING_AFTER_INACTIVITY;
                }
            }
        }

        return !shouldCreateRelease && this.ReleaseSkipped(repoContext: repoContext, skippingReason: skippingReason);
    }

    private static bool HasPendingDependencyUpdateBranches(in RepoContext repoContext)
    {
        IReadOnlyCollection<string> branches = repoContext.Repository.GetRemoteBranches(
            upstream: GitConstants.Upstream
        );

        return branches.Any(BranchClassification.IsDependencyBranch);
    }

    private static int ScoreCount(string releaseNotes, Regex regex, int scoreMultiplier)
    {
        return regex.Matches(releaseNotes).Count * scoreMultiplier;
    }

    private int IsAllAutoUpdates(in RepoContext repoContext, string releaseNotes, IReadOnlyList<PackageUpdate> packages)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return 0;
        }

        int updateCount = 0;

        updateCount += ScoreCount(
            releaseNotes: releaseNotes,
            ChangeLogParsingRegex.GeoIp(),
            scoreMultiplier: ScoreMultipliers.GeoIp
        );

        updateCount += ScoreCount(
            releaseNotes: releaseNotes,
            ChangeLogParsingRegex.DotNetSdk(),
            scoreMultiplier: ScoreMultipliers.DotNet
        );

        foreach (
            string packageId in ChangeLogParsingRegex
                .Dependencies()
                .Matches(releaseNotes)
                .Select(packageMatch => packageMatch.Groups["PackageId"].Value)
        )
        {
            if (IsPackageConsideredForVersionUpdate(packageUpdates: packages, packageId: packageId))
            {
                this._logger.LogMatchedPackage(
                    repoContext: repoContext,
                    packageId: packageId,
                    score: ScoreMultipliers.MatchedPackage
                );
                updateCount += ScoreMultipliers.MatchedPackage;
            }
            else
            {
                this._logger.LogIgnoredPackage(
                    repoContext: repoContext,
                    packageId: packageId,
                    score: ScoreMultipliers.IgnoredPackage
                );
                updateCount += ScoreMultipliers.IgnoredPackage;
            }
        }

        return updateCount;
    }

    private static bool IsPackageConsideredForVersionUpdate(
        IReadOnlyList<PackageUpdate> packageUpdates,
        string packageId
    )
    {
        string candidate = packageId.TrimEnd('.');

        return packageUpdates
            .Where(IsMatch)
            .Select(package => !package.ProhibitVersionBumpWhenReferenced)
            .FirstOrDefault(true);

        bool IsMatch(PackageUpdate package)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x: candidate, y: package.PackageId);
        }
    }

    private string GetNextVersion(in RepoContext repoContext)
    {
        NuGetVersion version = this._versionDetector.FindVersion(
            repository: repoContext.Repository.Active,
            buildNumber: DEFAULT_BUILD_NUMBER
        );

        this._logger.LogLastRelease(version);

        NuGetVersion nextVersion = new(major: version.Major, minor: version.Minor, patch: version.Patch);
        this._logger.LogNextRelease(version);

        bool exists = repoContext.Repository.DoesBranchExist($"release/{nextVersion}");

        while (exists)
        {
            nextVersion = new(major: version.Major, minor: version.Minor, version.Patch + 1);
            this._logger.LogNextRelease(version);
            exists = repoContext.Repository.DoesBranchExist($"release/{nextVersion}");
        }

        return nextVersion.ToString();
    }

    private static class ScoreMultipliers
    {
        public const int MatchedPackage = 1;
        public const int IgnoredPackage = 0;
        public const int GeoIp = 1;
        public const int DotNet = 1000;
    }
}
