using System;
using System.Collections.Generic;
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

internal static class ReleaseGeneration
{
    private const int DEFAULT_BUILD_NUMBER = 101;
    private const string UPSTREAM = "upstream";

    public static async ValueTask TryCreateNextPatchAsync(string repo,
                                                          Repository repository,
                                                          string changeLogFile,
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

        if (repo.Contains(value: "template", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // *********************************************************
        // * 2 RELEASE NOTES AND DURATION
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

            return;
        }

        // *********************************************************
        // * 3 CODE QUALITY AND BUILD

        try
        {
            await SolutionCheck.ReleaseCheckAsync(solutions: solutions, logger: logger, cancellationToken: cancellationToken);
        }
        catch (SolutionCheckFailedException)
        {
            Skip(repo: repo, skippingReason: "FAILED RELEASE CHECK");

            return;
        }

        try
        {
            await DotNetBuild.BuildAsync(basePath: basePath, buildSettings: buildSettings, logger: logger, cancellationToken: cancellationToken);
        }
        catch (DotNetBuildErrorException)
        {
            Skip(repo: repo, skippingReason: "DOES NOT BUILD");
        }

        // *********************************************************
        // * 4 Dispatch

        bool hasPendingDependencyUpdateBranches = HasPendingDependencyUpdateBranches(repository);

        if (!hasPendingDependencyUpdateBranches)
        {
            if (ShouldAlwaysCreatePatchRelease(repo))
            {
                await CreateAsync(repo: repo, repository: repository, changeLogFile: changeLogFile, versionDetector: versionDetector, cancellationToken: cancellationToken);
            }
            else
            {
                bool allowUpdates = CheckRepoForAllowedAutoUpgrade(repo);

                if (allowUpdates)
                {
                    if (!buildSettings.Publishable)
                    {
                        await CreateAsync(repo: repo, repository: repository, changeLogFile: changeLogFile, versionDetector: versionDetector, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        if (ShouldAlwaysCreatePatchRelease(repo))
                        {
                            await CreateAsync(repo: repo, repository: repository, changeLogFile: changeLogFile, versionDetector: versionDetector, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            Skip(repo: repo, skippingReason: "CONTAINS PUBLISHABLE EXECUTABLES");
                        }
                    }
                }
                else
                {
                    Skip(repo: repo, skippingReason: "EXPLICITLY PROHIBITED");
                }
            }
        }
        else
        {
            Skip(repo: repo, skippingReason: "FOUND PENDING UPDATE BRANCHES");
        }
    }

    private static bool CheckRepoForAllowedAutoUpgrade(string repo)
    {
        if (StringComparer.Ordinal.Equals(x: repo, y: "git@github.com:funfair-tech/funfair-server-content-package.git"))
        {
            return false;
        }

        if (repo.Contains(value: "code-analysis", comparisonType: StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldAlwaysCreatePatchRelease(string repo)
    {
        if (repo.Contains(value: "template", comparisonType: StringComparison.Ordinal))
        {
            return false;
        }

        if (repo.Contains(value: "credfeto", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        if (repo.Contains(value: "BuildBot", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (repo.Contains(value: "CoinBot", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (repo.Contains(value: "funfair-server-balance-bot", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        if (repo.Contains(value: "funfair-build-check", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        if (repo.Contains(value: "funfair-build-version", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        if (repo.Contains(value: "funfair-content-package-builder", comparisonType: StringComparison.Ordinal))
        {
            return true;
        }

        return false;
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

    public static async ValueTask CreateAsync(string repo, Repository repository, string changeLogFile, IVersionDetector versionDetector, CancellationToken cancellationToken)
    {
        string nextVersion = GetNextVersion(repository: repository, versionDetector: versionDetector);

        await ChangeLogUpdater.CreateReleaseAsync(changeLogFileName: changeLogFile, version: nextVersion, pending: false, cancellationToken: cancellationToken);

        await GitUtils.CommitAsync(repo: repository, $"Changelog for {nextVersion}", cancellationToken: cancellationToken);
        await GitUtils.PushAsync(repo: repository, cancellationToken: cancellationToken);

        string releaseBranch = $"release/{nextVersion}";
        GitUtils.CreateBranch(repo: repository, branchName: releaseBranch);
        await GitUtils.PushOriginAsync(repo: repository, branchName: releaseBranch, upstream: UPSTREAM, cancellationToken: cancellationToken);

        throw new ReleaseCreatedException($"Releases {nextVersion} created for {repo}");
    }

    private static int IsAllAutoUpdates(string releaseNotes, IReadOnlyList<PackageUpdate> packages)
    {
        const RegexOptions regexOptions = RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;
        TimeSpan regexTimeout = TimeSpan.FromSeconds(value: 1);

        Regex packageUpdateRegex = new(pattern: @"^\s*\-\s*Dependencies\s*\-\s*Updated\s+(?<PackageId>.+(\.+)*?)\sto\s+(\d+\..*)$", options: regexOptions, matchTimeout: regexTimeout);

        int updateCount = 0;

        bool hasContent = false;

        foreach (string line in releaseNotes.Split(Environment.NewLine))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            hasContent = true;

            Match packageMatches = packageUpdateRegex.Match(line);

            if (packageMatches.Success)
            {
                // Package Update
                string packageId = packageMatches.Groups["PackageId"].Value;

                if (IsPackageConsideredForVersionUpdate(packageUpdates: packages, packageId: packageId))
                {
                    //Log -message "Found Matching Update: $packageName"
                    updateCount++;
                }

                //Log -message "Skipping Ignored Update: $packageName"
                continue;
            }

            if (Regex.IsMatch(input: line, pattern: "^\\s*\\-\\s*GEOIP\\s*\\-\\s*", options: regexOptions, matchTimeout: regexTimeout))
            {
                // GEO-IP update
                updateCount++;

                continue;
            }

            if (line.StartsWith(value: "- SDK - Updated DotNet SDK to ", comparisonType: StringComparison.Ordinal))
            {
                // Dotnet version update
                updateCount += 1000;
            }
        }

        if (hasContent)
        {
            return updateCount;
        }

        return 0;
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

    private static class ReleaseSettings
    {
        public const int AutoReleasePendingPackages = 2;
        public const double MinimumHoursBeforeAutoRelease = 4;
        public const double InactivityHoursBeforeAutoRelease = 2 * MinimumHoursBeforeAutoRelease;
    }
}