using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.Date.Interfaces;
using Credfeto.DotNet.Repo.Git;
using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.Build.Exceptions;
using Credfeto.DotNet.Repo.Tools.Cmd.Exceptions;
using Credfeto.DotNet.Repo.Tools.Cmd.Models;
using Credfeto.DotNet.Repo.Tools.Cmd.Packages;
using Credfeto.DotNet.Repo.Tools.DotNet;
using Credfeto.DotNet.Repo.Tracking;
using FunFair.BuildVersion.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.Cmd.BumpRelease.Services;

public sealed class ReleaseGeneration : IReleaseGeneration
{
    private const int DEFAULT_BUILD_NUMBER = 101;

    private static readonly IReadOnlyList<RepoMatch> AllowedAutoUpgrade =
    [
        new(Repo: "git@github.com:funfair-tech/funfair-server-content-package.git", MatchType: MatchType.EXACT, Include: false),
        new(Repo: "code-analysis", MatchType: MatchType.CONTAINS, Include: false)
    ];

    private static readonly IReadOnlyList<RepoMatch> AlwaysMatch =
    [
        new(Repo: "template", MatchType: MatchType.CONTAINS, Include: false),
        new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "BuildBot", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "CoinBot", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-server-balance-bot", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-build-check", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-build-version", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-content-package-builder", MatchType: MatchType.CONTAINS, Include: true)
    ];

    private static readonly IReadOnlyList<RepoMatch> NeverRelease =
    [
        new(Repo: "template", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "git@github.com:funfair-tech/funfair-server-content-package.git", MatchType: MatchType.EXACT, Include: true)
    ];

    private readonly IDotNetBuild _dotNetBuild;

    private readonly ILogger<ReleaseGeneration> _logger;
    private readonly ISolutionCheck _solutionCheck;
    private readonly ICurrentTimeSource _timeSource;
    private readonly ITrackingCache _trackingCache;
    private readonly IVersionDetector _versionDetector;

    public ReleaseGeneration(ICurrentTimeSource timeSource,
                             IVersionDetector versionDetector,
                             ITrackingCache trackingCache,
                             ISolutionCheck solutionCheck,
                             IDotNetBuild dotNetBuild,
                             ILogger<ReleaseGeneration> logger)
    {
        this._timeSource = timeSource;
        this._versionDetector = versionDetector;
        this._trackingCache = trackingCache;
        this._solutionCheck = solutionCheck;
        this._dotNetBuild = dotNetBuild;
        this._logger = logger;
    }

    public async ValueTask TryCreateNextPatchAsync(RepoContext repoContext,
                                                   string basePath,
                                                   BuildSettings buildSettings,
                                                   DotNetVersionSettings dotNetSettings,
                                                   IReadOnlyList<string> solutions,
                                                   IReadOnlyList<PackageUpdate> packages,
                                                   CancellationToken cancellationToken)
    {
        // *********************************************************
        // * 1 TEMPLATE REPOS

        if (ShouldNeverAutoReleaseRepo(repoContext))
        {
            return;
        }

        // *********************************************************
        // * 2 RELEASE NOTES AND DURATION
        if (await this.ShouldNeverReleaseTimeAndContentBasedAsync(repoContext: repoContext, packages: packages, cancellationToken: cancellationToken))
        {
            return;
        }

        // *********************************************************
        // * 3 CODE QUALITY AND BUILD

        if (await this.ShouldNeverReleaseCodeQualityAsync(repoContext: repoContext,
                                                          basePath: basePath,
                                                          buildSettings: buildSettings,
                                                          solutions: solutions,
                                                          dotNetSettings: dotNetSettings,
                                                          cancellationToken: cancellationToken))
        {
            return;
        }

        // *********************************************************
        // * 4 Dispatch

        if (this.ShouldNeverReleaseFuzzyRules(repoContext: repoContext, buildSettings: buildSettings))
        {
            return;
        }

        await this.CreateAsync(repoContext: repoContext, cancellationToken: cancellationToken);
    }

    public async ValueTask CreateAsync(RepoContext repoContext, CancellationToken cancellationToken)
    {
        string nextVersion = this.GetNextVersion(repoContext: repoContext);

        await ChangeLogUpdater.CreateReleaseAsync(changeLogFileName: repoContext.ChangeLogFileName, version: nextVersion, pending: false, cancellationToken: cancellationToken);

        await repoContext.Repository.CommitAsync($"Changelog for {nextVersion}", cancellationToken: cancellationToken);
        await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);

        this._logger.LogInformation($"{repoContext.ClonePath}: RELEASE CREATED: {nextVersion}");

        this._trackingCache.Set(repoUrl: repoContext.ClonePath, value: repoContext.Repository.HeadRev);

        string releaseBranch = $"release/{nextVersion}";
        await repoContext.Repository.CreateBranchAsync(branchName: releaseBranch, cancellationToken: cancellationToken);
        await repoContext.Repository.PushOriginAsync(branchName: releaseBranch, upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

        throw new ReleaseCreatedException($"Releases {nextVersion} created for {repoContext.ClonePath}");
    }

    private bool ShouldNeverReleaseFuzzyRules(in RepoContext repoContext, in BuildSettings buildSettings)
    {
        if (HasPendingDependencyUpdateBranches(repoContext))
        {
            this.Skip(repoContext: repoContext, skippingReason: "FOUND PENDING UPDATE BRANCHES");

            return true;
        }

        if (ShouldAlwaysCreatePatchRelease(repoContext))
        {
            return false;
        }

        if (CheckRepoForAllowedAutoUpgrade(repoContext))
        {
            if (!buildSettings.Publishable)
            {
                return false;
            }

            this.Skip(repoContext: repoContext, skippingReason: "CONTAINS PUBLISHABLE EXECUTABLES");

            return true;
        }

        this.Skip(repoContext: repoContext, skippingReason: "EXPLICITLY PROHIBITED");

        return true;
    }

    private async ValueTask<bool> ShouldNeverReleaseCodeQualityAsync(RepoContext repoContext,
                                                                     string basePath,
                                                                     BuildSettings buildSettings,
                                                                     DotNetVersionSettings dotNetSettings,
                                                                     IReadOnlyList<string> solutions,
                                                                     CancellationToken cancellationToken)
    {
        try
        {
            await this._solutionCheck.ReleaseCheckAsync(solutions: solutions, dotNetSettings: dotNetSettings, cancellationToken: cancellationToken);
        }
        catch (SolutionCheckFailedException)
        {
            this.Skip(repoContext: repoContext, skippingReason: "FAILED RELEASE CHECK");

            return true;
        }

        try
        {
            await this._dotNetBuild.BuildAsync(basePath: basePath, buildSettings: buildSettings, cancellationToken: cancellationToken);
        }
        catch (DotNetBuildErrorException)
        {
            this.Skip(repoContext: repoContext, skippingReason: "DOES NOT BUILD");

            return true;
        }

        return false;
    }

    private async ValueTask<bool> ShouldNeverReleaseTimeAndContentBasedAsync(RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        string releaseNotes =
            await ChangeLogReader.ExtractReleaseNotesFromFileAsync(changeLogFileName: repoContext.ChangeLogFileName, version: "Unreleased", cancellationToken: cancellationToken);

        int autoUpdateCount = this.IsAllAutoUpdates(releaseNotes: releaseNotes, packages: packages);

        this._logger.LogInformation($"Change log update score: {autoUpdateCount}");

        DateTimeOffset lastCommitDate = repoContext.Repository.GetLastCommitDate();
        DateTimeOffset now = this._timeSource.UtcNow();
        TimeSpan timeSinceLastCommit = now - lastCommitDate;

        string skippingReason = "INSUFFICIENT UPDATES";
        bool shouldCreateRelease = false;

        if (autoUpdateCount > ReleaseSettings.AutoReleasePendingPackages)
        {
            if (timeSinceLastCommit.TotalHours > ReleaseSettings.MinimumHoursBeforeAutoRelease)
            {
                shouldCreateRelease = true;
                skippingReason = "RELEASING NORMAL";
            }
            else
            {
                skippingReason = "INSUFFICIENT DURATION SINCE LAST UPDATE";
            }
        }

        if (!shouldCreateRelease)
        {
            if (autoUpdateCount >= 1)
            {
                if (timeSinceLastCommit.TotalHours > ReleaseSettings.InactivityHoursBeforeAutoRelease)
                {
                    shouldCreateRelease = true;
                    skippingReason = $"RELEASING AFTER INACTIVITY : {autoUpdateCount}";
                }
            }
        }

        if (!shouldCreateRelease)
        {
            this.Skip(repoContext: repoContext, skippingReason: skippingReason);

            return true;
        }

        return false;
    }

    private static bool ShouldNeverAutoReleaseRepo(RepoContext repoContext)
    {
        return NeverRelease.Where(match => match.IsMatch(repoContext.ClonePath))
                           .Select(match => match.Include)
                           .FirstOrDefault();
    }

    private static bool CheckRepoForAllowedAutoUpgrade(RepoContext repoContext)
    {
        return AllowedAutoUpgrade.Where(match => match.IsMatch(repoContext.ClonePath))
                                 .Select(match => match.Include)
                                 .FirstOrDefault(true);
    }

    private static bool ShouldAlwaysCreatePatchRelease(RepoContext repoContext)
    {
        return AlwaysMatch.Where(match => match.IsMatch(repoContext.ClonePath))
                          .Select(match => match.Include)
                          .FirstOrDefault();
    }

    private static bool HasPendingDependencyUpdateBranches(in RepoContext repoContext)
    {
        IReadOnlyCollection<string> branches = repoContext.Repository.GetRemoteBranches(upstream: GitConstants.Upstream);

        return branches.Any(IsDependencyBranch);

        static bool IsDependencyBranch(string branch)
        {
            return IsPackageUpdaterBranch(branch) || IsDependabotBranch(branch);
        }

        static bool IsPackageUpdaterBranch(string branch)
        {
            return branch.StartsWith(value: "depends/", comparisonType: StringComparison.Ordinal) &&
                   !branch.StartsWith(value: "/preview/", comparisonType: StringComparison.Ordinal);
        }

        static bool IsDependabotBranch(string branch)
        {
            return branch.StartsWith(value: "dependabot/", comparisonType: StringComparison.Ordinal);
        }
    }

    private void Skip(in RepoContext repoContext, string skippingReason)
    {
        this._logger.LogInformation($"{repoContext.ClonePath}: RELEASE SKIPPED: {skippingReason}");
    }

    private static int ScoreCount(string releaseNotes, Regex regex, int scoreMultiplier)
    {
        return regex.Matches(releaseNotes)
                    .Count * scoreMultiplier;
    }

    private int IsAllAutoUpdates(string releaseNotes, IReadOnlyList<PackageUpdate> packages)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return 0;
        }

        int updateCount = 0;

        updateCount += ScoreCount(releaseNotes: releaseNotes, ChangeLogParsingRegex.GeoIp(), scoreMultiplier: ScoreMultipliers.GeoIp);

        updateCount += ScoreCount(releaseNotes: releaseNotes, ChangeLogParsingRegex.DotNetSdk(), scoreMultiplier: ScoreMultipliers.DotNet);

        foreach (string packageId in ChangeLogParsingRegex.Dependencies()
                                                          .Matches(releaseNotes)
                                                          .Select(packageMatch => packageMatch.Groups["PackageId"].Value))
        {
            if (IsPackageConsideredForVersionUpdate(packageUpdates: packages, packageId: packageId))
            {
                this._logger.LogInformation($"Found Matching Update: {packageId}");
                updateCount += ScoreMultipliers.MatchedPackage;
            }
            else
            {
                this._logger.LogInformation($"Skipping Ignored Update: {packageId}");
                updateCount += ScoreMultipliers.IgnoredPackage;
            }
        }

        return updateCount;
    }

    private static bool IsPackageConsideredForVersionUpdate(IReadOnlyList<PackageUpdate> packageUpdates, string packageId)
    {
        string candidate = packageId.TrimEnd('.');

        return packageUpdates.Where(IsMatch)
                             .Select(package => !package.ProhibitVersionBumpWhenReferenced)
                             .FirstOrDefault(true);

        bool IsMatch(PackageUpdate package)
        {
            return StringComparer.InvariantCultureIgnoreCase.Equals(x: candidate, y: package.PackageId);
        }
    }

    private string GetNextVersion(in RepoContext repoContext)
    {
        NuGetVersion version = this._versionDetector.FindVersion(repository: repoContext.Repository.Active, buildNumber: DEFAULT_BUILD_NUMBER);

        return new NuGetVersion(major: version.Major, minor: version.Minor, version.Patch + 1).ToString();
    }

    private static class ScoreMultipliers
    {
        public const int MatchedPackage = 1;
        public const int IgnoredPackage = 0;
        public const int GeoIp = 1;
        public const int DotNet = 1000;
    }

    private enum MatchType
    {
        EXACT,
        CONTAINS
    }

    [DebuggerDisplay("{Repo}: {MatchType} Include : {Include}")]
    private readonly record struct RepoMatch(string Repo, MatchType MatchType, bool Include)
    {
        public bool IsMatch(string repo)
        {
            return this.MatchType == MatchType.EXACT
                ? StringComparer.OrdinalIgnoreCase.Equals(x: repo, y: this.Repo)
                : repo.Contains(value: this.Repo, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
    }

    private static class ReleaseSettings
    {
        public const int AutoReleasePendingPackages = 2;
        public const double MinimumHoursBeforeAutoRelease = 4;
        public const double InactivityHoursBeforeAutoRelease = 2 * MinimumHoursBeforeAutoRelease;
    }
}