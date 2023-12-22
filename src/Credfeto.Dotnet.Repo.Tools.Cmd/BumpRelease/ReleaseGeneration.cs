using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.Date.Interfaces;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tools.Cmd.Build;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Dotnet.Repo.Tools.Cmd.Packages;
using FunFair.BuildVersion.Interfaces;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.BumpRelease;

// TODO: Make Class non-static and use DI
internal static class ReleaseGeneration
{
    private const int DEFAULT_BUILD_NUMBER = 101;
    private const string UPSTREAM = "upstream";

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
        new(Repo: "git@github.com:funfair-tech/funfair-server-content-package.git", MatchType: MatchType.EXACT, Include: true),
    ];

    public static async ValueTask TryCreateNextPatchAsync(string repo,
                                                          Repository repository,
                                                          string changeLogFileName,
                                                          string basePath,
                                                          BuildSettings buildSettings,
                                                          IReadOnlyList<string> solutions,
                                                          IReadOnlyList<PackageUpdate> packages,
                                                          ICurrentTimeSource timeSource,
                                                          IVersionDetector versionDetector,
                                                          ILogger logger,
                                                          CancellationToken cancellationToken)
    {
        // *********************************************************
        // * 1 TEMPLATE REPOS

        if (ShouldNeverAutoReleaseRepo(repo))
        {
            return;
        }

        // *********************************************************
        // * 2 RELEASE NOTES AND DURATION
        if (await ShouldNeverReleaseTimeAndContentBasedAsync(repo: repo,
                                                             repository: repository,
                                                             changeLogFile: changeLogFileName,
                                                             packages: packages,
                                                             timeSource: timeSource,
                                                             cancellationToken: cancellationToken))
        {
            return;
        }

        // *********************************************************
        // * 3 CODE QUALITY AND BUILD

        if (await ShouldNeverReleaseCodeQualityAsync(repo: repo, basePath: basePath, buildSettings: buildSettings, solutions: solutions, logger: logger, cancellationToken: cancellationToken))
        {
            return;
        }

        // *********************************************************
        // * 4 Dispatch

        if (ShouldNeverReleaseFuzzyRules(repo: repo, repository: repository, buildSettings: buildSettings))
        {
            return;
        }

        await CreateAsync(repo: repo, repository: repository, changeLogFileName: changeLogFileName, versionDetector: versionDetector, cancellationToken: cancellationToken);
    }

    private static bool ShouldNeverReleaseFuzzyRules(string repo, Repository repository, in BuildSettings buildSettings)
    {
        if (HasPendingDependencyUpdateBranches(repository))
        {
            Skip(repo: repo, skippingReason: "FOUND PENDING UPDATE BRANCHES");

            return true;
        }

        if (ShouldAlwaysCreatePatchRelease(repo))
        {
            return false;
        }

        if (CheckRepoForAllowedAutoUpgrade(repo))
        {
            if (!buildSettings.Publishable)
            {
                return false;
            }

            Skip(repo: repo, skippingReason: "CONTAINS PUBLISHABLE EXECUTABLES");

            return true;
        }

        Skip(repo: repo, skippingReason: "EXPLICITLY PROHIBITED");

        return true;
    }

    private static async ValueTask<bool> ShouldNeverReleaseCodeQualityAsync(string repo,
                                                                            string basePath,
                                                                            BuildSettings buildSettings,
                                                                            IReadOnlyList<string> solutions,
                                                                            ILogger logger,
                                                                            CancellationToken cancellationToken)
    {
        try
        {
            await SolutionCheck.ReleaseCheckAsync(solutions: solutions, logger: logger, cancellationToken: cancellationToken);
        }
        catch (SolutionCheckFailedException)
        {
            Skip(repo: repo, skippingReason: "FAILED RELEASE CHECK");

            return true;
        }

        try
        {
            await DotNetBuild.BuildAsync(basePath: basePath, buildSettings: buildSettings, logger: logger, cancellationToken: cancellationToken);
        }
        catch (DotNetBuildErrorException)
        {
            Skip(repo: repo, skippingReason: "DOES NOT BUILD");

            return true;
        }

        return false;
    }

    private static async ValueTask<bool> ShouldNeverReleaseTimeAndContentBasedAsync(string repo,
                                                                                    Repository repository,
                                                                                    string changeLogFile,
                                                                                    IReadOnlyList<PackageUpdate> packages,
                                                                                    ICurrentTimeSource timeSource,
                                                                                    CancellationToken cancellationToken)
    {
        string releaseNotes = await ChangeLogReader.ExtractReleaseNodesFromFileAsync(changeLogFileName: changeLogFile, version: "Unreleased", cancellationToken: cancellationToken);

        int autoUpdateCount = IsAllAutoUpdates(releaseNotes: releaseNotes, packages: packages);

        DateTimeOffset lastCommitDate = GitUtils.GetLastCommitDate(repository);
        DateTimeOffset now = timeSource.UtcNow();
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
            Skip(repo: repo, skippingReason: skippingReason);

            return true;
        }

        return false;
    }

    private static bool ShouldNeverAutoReleaseRepo(string repo)
    {
        return NeverRelease.Where(match => match.IsMatch(repo))
                           .Select(match => match.Include)
                           .FirstOrDefault();
    }

    private static bool CheckRepoForAllowedAutoUpgrade(string repo)
    {
        return AllowedAutoUpgrade.Where(match => match.IsMatch(repo))
                                 .Select(match => match.Include)
                                 .FirstOrDefault(true);
    }

    private static bool ShouldAlwaysCreatePatchRelease(string repo)
    {
        return AlwaysMatch.Where(match => match.IsMatch(repo))
                          .Select(match => match.Include)
                          .FirstOrDefault();
    }

    private static bool HasPendingDependencyUpdateBranches(Repository repository)
    {
        IReadOnlyCollection<string> branches = GitUtils.GetRemoteBranches(repo: repository, upstream: UPSTREAM);

        return branches.Any(IsDependencyBranch);

        static bool IsDependencyBranch(string branch)
        {
            return branch.StartsWith(value: "depends/", comparisonType: StringComparison.Ordinal) && !branch.StartsWith(value: "/preview/", comparisonType: StringComparison.Ordinal);
        }
    }

    private static void Skip(string repo, string skippingReason)
    {
        // TODO Log
    }

    public static async ValueTask CreateAsync(string repo, Repository repository, string changeLogFileName, IVersionDetector versionDetector, CancellationToken cancellationToken)
    {
        string nextVersion = GetNextVersion(repository: repository, versionDetector: versionDetector);

        await ChangeLogUpdater.CreateReleaseAsync(changeLogFileName: changeLogFileName, version: nextVersion, pending: false, cancellationToken: cancellationToken);

        await GitUtils.CommitAsync(repo: repository, $"Changelog for {nextVersion}", cancellationToken: cancellationToken);
        await GitUtils.PushAsync(repo: repository, cancellationToken: cancellationToken);

        string releaseBranch = $"release/{nextVersion}";
        GitUtils.CreateBranch(repo: repository, branchName: releaseBranch);
        await GitUtils.PushOriginAsync(repo: repository, branchName: releaseBranch, upstream: UPSTREAM, cancellationToken: cancellationToken);

        throw new ReleaseCreatedException($"Releases {nextVersion} created for {repo}");
    }

    private static int IsAllAutoUpdates(string releaseNotes, IReadOnlyList<PackageUpdate> packages)
    {
        int updateCount = 0;

        updateCount += ChangeLogParsingRegex.GeoIp()
                                            .Matches(releaseNotes)
                                            .Count + ScoreMultipliers.GeoIp;

        updateCount += ChangeLogParsingRegex.DotNetSdk()
                                            .Matches(releaseNotes)
                                            .Count * ScoreMultipliers.DotNet;

        foreach (Match packageMatch in ChangeLogParsingRegex.Dependencies()
                                                            .Matches(releaseNotes))
        {
            // Package Update
            string packageId = packageMatch.Groups["PackageId"].Value;

            if (IsPackageConsideredForVersionUpdate(packageUpdates: packages, packageId: packageId))
            {
                //Log -message "Found Matching Update: $packageName"
                updateCount += ScoreMultipliers.MatchedPackage;
            }
            else
            {
                updateCount += ScoreMultipliers.IgnoredPackage;
            }

            //Log -message "Skipping Ignored Update: $packageName"
        }

        return updateCount;
    }

    private static bool IsPackageConsideredForVersionUpdate(IReadOnlyList<PackageUpdate> packageUpdates, string packageId)
    {
        return packageUpdates.Where(package => StringComparer.InvariantCultureIgnoreCase.Equals(packageId.TrimEnd('.'), y: package.PackageId))
                             .Select(package => !package.ProhibitVersionBumpWhenReferenced)
                             .FirstOrDefault(true);
    }

    private static string GetNextVersion(Repository repository, IVersionDetector versionDetector)
    {
        NuGetVersion version = versionDetector.FindVersion(repository: repository, buildNumber: DEFAULT_BUILD_NUMBER);

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