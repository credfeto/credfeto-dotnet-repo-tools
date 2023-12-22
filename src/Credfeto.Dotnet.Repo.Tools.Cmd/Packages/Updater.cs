using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tools.Cmd.Build;
using Credfeto.Dotnet.Repo.Tools.Cmd.BumpRelease;
using Credfeto.Dotnet.Repo.Tools.Cmd.DotNet;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Package;
using FunFair.BuildCheck.Runner.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

// TODO: Make Class non-static and use DI
internal static class Updater
{
    private const string UPSTREAM = "origin";

    private const string CHANGELOG_ENTRYTYPE = "Changed";

    public static async Task UpdateRepositoriesAsync(UpdateContext updateContext,
                                                     IReadOnlyList<string> repos,
                                                     IReadOnlyList<PackageUpdate> packages,
                                                     IPackageUpdater packageUpdater,
                                                     IPackageCache packageCache,
                                                     ILogger logger,
                                                     CancellationToken cancellationToken)
    {
        try
        {
            foreach (string repo in repos)
            {
                try
                {
                    await UpdateRepositoryAsync(updateContext: updateContext, logger: logger, packages: packages, packageUpdater: packageUpdater, cancellationToken: cancellationToken, repo: repo);
                }
                catch (SolutionCheckFailedException exception)
                {
                    logger.LogError(exception: exception, message: "Solution check failed");
                }
                catch (DotNetBuildErrorException exception)
                {
                    logger.LogError(exception: exception, message: "Build failed");
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(updateContext.Cache))
                    {
                        await packageCache.SaveAsync(fileName: updateContext.Cache, cancellationToken: cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(updateContext.Tracking))
                    {
                        await updateContext.TrackingCache.SaveAsync(fileName: updateContext.Tracking, cancellationToken: cancellationToken);
                    }
                }
            }
        }
        catch (ReleaseCreatedException exception)
        {
            logger.LogInformation(exception: exception, message: "Release created - aborting run");
            logger.LogInformation(exception.Message);
        }
    }

    private static async Task UpdateRepositoryAsync(UpdateContext updateContext,
                                                    IReadOnlyList<PackageUpdate> packages,
                                                    IPackageUpdater packageUpdater,
                                                    string repo,
                                                    ILogger logger,
                                                    CancellationToken cancellationToken)
    {
        logger.LogInformation($"Processing {repo}");

        using (Repository repository = await GitUtils.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            string repoUrl = GitUtils.GetUrl(repository, GitConstants.Upstream);

            if (!ChangeLogDetector.TryFindChangeLog(repository: repository, out string? changeLogFileName))
            {
                logger.LogInformation("No changelog found");
                await updateContext.UpdateTrackingAsync(repo: repoUrl, GitUtils.GetHeadRev(repository), cancellationToken: cancellationToken);

                return;
            }

            await ProcessRepoUpdatesAsync(updateContext: updateContext,
                                          packages: packages,
                                          packageUpdater: packageUpdater,
                                          changeLogFileName: changeLogFileName,
                                          logger: logger,
                                          cancellationToken: cancellationToken,
                                          repository: repository);
        }
    }

    private static async Task ProcessRepoUpdatesAsync(UpdateContext updateContext,
                                                      IReadOnlyList<PackageUpdate> packages,
                                                      IPackageUpdater packageUpdater,
                                                      Repository repository,
                                                      string changeLogFileName,
                                                      ILogger logger,
                                                      CancellationToken cancellationToken)
    {
        string repoUrl = GitUtils.GetUrl(repository, GitConstants.Upstream);
        string? lastKnownGoodBuild = updateContext.TrackingCache.Get(repoUrl);

        if (!HasDotNetFiles(repository: repository, out string? sourceDirectory, out IReadOnlyList<string>? solutions, out IReadOnlyList<string>? projects))
        {
            logger.LogInformation("No dotnet files found");
            await updateContext.UpdateTrackingAsync(repo: repoUrl, GitUtils.GetHeadRev(repository), cancellationToken: cancellationToken);

            return;
        }

        BuildSettings buildSettings = DotNetBuild.LoadBuildSettings(new ProjectLoader(), projects: projects);

        int totalUpdates = 0;

        foreach (PackageUpdate package in packages)
        {
            (bool updated, lastKnownGoodBuild) = await ProcessRepoOnePackageUpdateAsync(updateContext: updateContext,
                                                                                        packageUpdater: packageUpdater,
                                                                                        repository: repository,
                                                                                        solutions: solutions,
                                                                                        sourceDirectory: sourceDirectory,
                                                                                        buildSettings: buildSettings,
                                                                                        dotNetSettings: updateContext.DotNetSettings,
                                                                                        package: package,
                                                                                        changeLogFileName: changeLogFileName,
                                                                                        lastKnownGoodBuild: lastKnownGoodBuild,
                                                                                        logger: logger,
                                                                                        cancellationToken: cancellationToken);

            if (updated)
            {
                ++totalUpdates;
            }
        }

        if (totalUpdates == 0)
        {
            // no updates in this run - so might be able to create a release
            await ReleaseGeneration.TryCreateNextPatchAsync(repository: repository,
                                                            changeLogFileName: changeLogFileName,
                                                            basePath: sourceDirectory,
                                                            buildSettings: buildSettings,
                                                            dotNetSettings: updateContext.DotNetSettings,
                                                            solutions: solutions,
                                                            packages: packages,
                                                            timeSource: updateContext.TimeSource,
                                                            versionDetector: updateContext.VersionDetector,
                                                            trackingCache: updateContext.TrackingCache,
                                                            logger: logger,
                                                            cancellationToken: cancellationToken);
        }
    }

    private static async Task<(bool updated, string? lastKnownGoodBuild)> ProcessRepoOnePackageUpdateAsync(UpdateContext updateContext,
                                                                                                           IPackageUpdater packageUpdater,
                                                                                                           Repository repository,
                                                                                                           IReadOnlyList<string> solutions,
                                                                                                           string sourceDirectory,
                                                                                                           BuildSettings buildSettings,
                                                                                                           DotNetVersionSettings dotNetSettings,
                                                                                                           PackageUpdate package,
                                                                                                           string changeLogFileName,
                                                                                                           string? lastKnownGoodBuild,
                                                                                                           ILogger logger,
                                                                                                           CancellationToken cancellationToken)
    {
        bool updated = false;

        if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, GitUtils.GetHeadRev(repository)))
        {
            await SolutionCheck.PreCheckAsync(solutions: solutions, dotNetSettings: dotNetSettings, logger: logger, cancellationToken: cancellationToken);

            await DotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, logger: logger, cancellationToken: cancellationToken);

            lastKnownGoodBuild = GitUtils.GetHeadRev(repository);
            await updateContext.UpdateTrackingAsync(repo: GitUtils.GetUrl(repository, GitConstants.Upstream), value: lastKnownGoodBuild, cancellationToken: cancellationToken);
        }

        IReadOnlyList<PackageVersion> updatesMade = await UpdatePackagesAsync(updateContext: updateContext,
                                                                              packageUpdater: packageUpdater,
                                                                              package: package,
                                                                              repository: repository,
                                                                              logger: logger,
                                                                              cancellationToken: cancellationToken);

        if (updatesMade.Count != 0)
        {
            updated = true;

            string? goodBuildCommit = await OnPackageUpdateAsync(updateContext: updateContext,
                                                                 solutions: solutions,
                                                                 sourceDirectory: sourceDirectory,
                                                                 buildSettings: buildSettings,
                                                                 updatesMade: updatesMade,
                                                                 repository: repository,
                                                                 package: package,
                                                                 changeLogFileName: changeLogFileName,
                                                                 logger: logger,
                                                                 cancellationToken: cancellationToken);

            return (updated, goodBuildCommit ?? lastKnownGoodBuild);
        }

        return (updated, lastKnownGoodBuild);
    }

    private static async ValueTask<string?> OnPackageUpdateAsync(UpdateContext updateContext,
                                                                 IReadOnlyList<string> solutions,
                                                                 string sourceDirectory,
                                                                 BuildSettings buildSettings,
                                                                 IReadOnlyList<PackageVersion> updatesMade,
                                                                 Repository repository,
                                                                 PackageUpdate package,
                                                                 string changeLogFileName,
                                                                 ILogger logger,
                                                                 CancellationToken cancellationToken)
    {
        bool ok = await PostUpdateCheckAsync(solutions: solutions,
                                             sourceDirectory: sourceDirectory,
                                             buildSettings: buildSettings,
                                             dotNetSettings: updateContext.DotNetSettings,
                                             logger: logger,
                                             cancellationToken: cancellationToken);

        NuGetVersion version = GetUpdateVersion(updatesMade);

        await CommitToRepositoryAsync(repository: repository, package: package, version.ToString(), changeLogFileName: changeLogFileName, builtOk: ok, cancellationToken: cancellationToken);

        if (ok)
        {
            string headRev = GitUtils.GetHeadRev(repository);

            await updateContext.UpdateTrackingAsync(repo: GitUtils.GetUrl(repository, GitConstants.Upstream), value: headRev, cancellationToken: cancellationToken);

            return headRev;
        }

        await GitUtils.ResetToMasterAsync(repo: repository, upstream: UPSTREAM, cancellationToken: cancellationToken);

        return null;
    }

    private static NuGetVersion GetUpdateVersion(IReadOnlyList<PackageVersion> updatesMade)
    {
        return updatesMade.Select(x => x.Version)
                          .OrderByDescending(x => x.Version)
                          .First();
    }

    private static async ValueTask CommitToRepositoryAsync(Repository repository, PackageUpdate package, string version, string changeLogFileName, bool builtOk, CancellationToken cancellationToken)
    {
        string branchPrefix = $"depends/update-{package.PackageId}/".ToLowerInvariant();
        string branchForUpdate = branchPrefix + version;

        if (builtOk)
        {
            await CommitChangeWithChangelogAsync(repository: repository, package: package, version: version, changeLogFileName: changeLogFileName, cancellationToken: cancellationToken);
            await GitUtils.PushAsync(repo: repository, cancellationToken: cancellationToken);
            await GitUtils.RemoveBranchesForPrefixAsync(repo: repository,
                                                        Guid.NewGuid()
                                                            .ToString(),
                                                        branchPrefix: branchPrefix,
                                                        upstream: UPSTREAM,
                                                        cancellationToken: cancellationToken);
        }
        else
        {
            if (GitUtils.DoesBranchExist(repo: repository, branchName: branchForUpdate))
            {
                // nothing to do - may already be a PR that's being worked on
                return;
            }

            GitUtils.CreateBranch(repo: repository, branchName: branchForUpdate);

            await CommitChangeWithChangelogAsync(repository: repository, package: package, version: version, changeLogFileName: changeLogFileName, cancellationToken: cancellationToken);
            await GitUtils.PushOriginAsync(repo: repository, branchName: branchForUpdate, upstream: UPSTREAM, cancellationToken: cancellationToken);
            await GitUtils.RemoveBranchesForPrefixAsync(repo: repository, branchForUpdate: branchForUpdate, branchPrefix: branchPrefix, upstream: UPSTREAM, cancellationToken: cancellationToken);
        }
    }

    private static async ValueTask CommitChangeWithChangelogAsync(Repository repository, PackageUpdate package, string version, string changeLogFileName, CancellationToken cancellationToken)
    {
        await ChangeLogUpdater.RemoveEntryAsync(changeLogFileName: changeLogFileName,
                                                type: CHANGELOG_ENTRYTYPE,
                                                $"Dependencies - Updated {package.PackageId} to ",
                                                cancellationToken: cancellationToken);
        await ChangeLogUpdater.AddEntryAsync(changeLogFileName: changeLogFileName,
                                             type: CHANGELOG_ENTRYTYPE,
                                             $"Dependencies - Updated {package.PackageId} to {version}",
                                             cancellationToken: cancellationToken);

        await GitUtils.CommitAsync(repo: repository, $"[Dependencies] Updating {package.PackageId} ({package.PackageType}) to {version}", cancellationToken: cancellationToken);
    }

    private static async Task<bool> PostUpdateCheckAsync(IReadOnlyList<string> solutions,
                                                         string sourceDirectory,
                                                         BuildSettings buildSettings,
                                                         DotNetVersionSettings dotNetSettings,
                                                         ILogger logger,
                                                         CancellationToken cancellationToken)
    {
        bool ok = false;

        try
        {
            bool checkOk = await SolutionCheck.PostCheckAsync(solutions: solutions, dotNetSettings: dotNetSettings, logger: logger, cancellationToken: cancellationToken);

            if (checkOk)
            {
                await DotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, logger: logger, cancellationToken: cancellationToken);

                ok = true;
            }
        }
        catch (DotNetBuildErrorException exception)
        {
            logger.LogError(exception: exception, message: "Build failed");
            ok = false;
        }

        return ok;
    }

    private static async ValueTask<IReadOnlyList<PackageVersion>> UpdatePackagesAsync(UpdateContext updateContext,
                                                                                      IPackageUpdater packageUpdater,
                                                                                      PackageUpdate package,
                                                                                      Repository repository,
                                                                                      ILogger logger,
                                                                                      CancellationToken cancellationToken)
    {
        logger.LogInformation($"* Updating {package.PackageId}...");
        PackageUpdateConfiguration config = BuildConfiguration(package);

        return await packageUpdater.UpdateAsync(basePath: repository.Info.WorkingDirectory,
                                                configuration: config,
                                                packageSources: updateContext.AdditionalSources,
                                                cancellationToken: cancellationToken);
    }

    private static bool HasDotNetFiles(Repository repository,
                                       [NotNullWhen(true)] out string? sourceDirectory,
                                       [NotNullWhen(true)] out IReadOnlyList<string>? solutions,
                                       [NotNullWhen(true)] out IReadOnlyList<string>? projects)
    {
        string sourceFolder = Path.Combine(path1: repository.Info.WorkingDirectory, path2: "src");

        if (!Directory.Exists(sourceFolder))
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        string[] foundSolutions = Directory.GetFiles(path: sourceFolder, searchPattern: "*.sln", searchOption: SearchOption.AllDirectories);

        if (foundSolutions.Length == 0)
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        string[] foundProjects = Directory.GetFiles(path: sourceFolder, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);

        if (foundProjects.Length == 0)
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        sourceDirectory = sourceFolder;
        solutions = foundSolutions;
        projects = foundProjects;

        return true;
    }

    private static PackageUpdateConfiguration BuildConfiguration(PackageUpdate package)
    {
        PackageMatch packageMatch = new(PackageId: package.PackageId, Prefix: !package.ExactMatch);
        Console.WriteLine($"Including {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");

        IReadOnlyList<PackageMatch> excludedPackages = GetExcludedPackages(package.Exclude ?? Array.Empty<PackageExclude>());

        return new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);
    }

    private static IReadOnlyList<PackageMatch> GetExcludedPackages(IReadOnlyList<PackageExclude> excludes)
    {
        if (excludes.Count == 0)
        {
            return Array.Empty<PackageMatch>();
        }

        List<PackageMatch> excludedPackages = [];

        foreach (PackageExclude exclude in excludes)
        {
            PackageMatch packageMatch = new(PackageId: exclude.PackageId, Prefix: !exclude.ExactMatch);

            excludedPackages.Add(packageMatch);

            Console.WriteLine($"Excluding {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");
        }

        return excludedPackages;
    }
}