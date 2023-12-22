using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tools.Cmd.Build;
using Credfeto.Dotnet.Repo.Tools.Cmd.BumpRelease;
using Credfeto.Dotnet.Repo.Tools.Cmd.DotNet;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Dotnet.Repo.Tools.Cmd.Models;
using Credfeto.Dotnet.Repo.Tracking;
using Credfeto.Package;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages.Services;

public sealed class Updater : IUpdater
{
    private const string UPSTREAM = "origin";

    private const string CHANGELOG_ENTRYTYPE = "Changed";
    private readonly IDotNetBuild _dotNetBuild;
    private readonly ILogger<Updater> _logger;
    private readonly IPackageCache _packageCache;
    private readonly IPackageUpdater _packageUpdater;
    private readonly IReleaseGeneration _releaseGeneration;
    private readonly ISolutionCheck _solutionCheck;
    private readonly ITrackingCache _trackingCache;

    public Updater(IPackageUpdater packageUpdater,
                   IPackageCache packageCache,
                   ITrackingCache trackingCache,
                   ISolutionCheck solutionCheck,
                   IDotNetBuild dotNetBuild,
                   IReleaseGeneration releaseGeneration,
                   ILogger<Updater> logger)
    {
        this._packageUpdater = packageUpdater;
        this._packageCache = packageCache;
        this._trackingCache = trackingCache;
        this._solutionCheck = solutionCheck;
        this._dotNetBuild = dotNetBuild;
        this._releaseGeneration = releaseGeneration;
        this._logger = logger;
    }

    public async ValueTask UpdateRepositoriesAsync(UpdateContext updateContext, IReadOnlyList<string> repositories, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        try
        {
            foreach (string repo in repositories)
            {
                try
                {
                    await this.UpdateRepositoryAsync(updateContext: updateContext, logger: this._logger, packages: packages, cancellationToken: cancellationToken, repo: repo);
                }
                catch (SolutionCheckFailedException exception)
                {
                    this._logger.LogError(exception: exception, message: "Solution check failed");
                }
                catch (DotNetBuildErrorException exception)
                {
                    this._logger.LogError(exception: exception, message: "Build failed");
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(updateContext.CacheFileName))
                    {
                        await this._packageCache.SaveAsync(fileName: updateContext.CacheFileName, cancellationToken: cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(updateContext.TrackingFileName))
                    {
                        await this._trackingCache.SaveAsync(fileName: updateContext.TrackingFileName, cancellationToken: cancellationToken);
                    }
                }
            }
        }
        catch (ReleaseCreatedException exception)
        {
            this._logger.LogInformation(exception: exception, message: "Release created - aborting run");
            this._logger.LogInformation(exception.Message);
        }
    }

    private async Task UpdateRepositoryAsync(UpdateContext updateContext, IReadOnlyList<PackageUpdate> packages, string repo, ILogger logger, CancellationToken cancellationToken)
    {
        this._logger.LogInformation($"Processing {repo}");

        using (Repository repository = await GitUtils.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            if (!ChangeLogDetector.TryFindChangeLog(repository: repository, out string? changeLogFileName))
            {
                logger.LogInformation("No changelog found");
                await this._trackingCache.UpdateTrackingAsync(new(ClonePath: repo, Repository: repository, ChangeLogFileName: "?"),
                                                              updateContext: updateContext,
                                                              GitUtils.GetHeadRev(repository),
                                                              cancellationToken: cancellationToken);

                return;
            }

            RepoContext repoContext = new(ClonePath: repo, Repository: repository, ChangeLogFileName: changeLogFileName);

            await this.ProcessRepoUpdatesAsync(updateContext: updateContext, repoContext: repoContext, packages: packages, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessRepoUpdatesAsync(UpdateContext updateContext, RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        string? lastKnownGoodBuild = this._trackingCache.Get(repoContext.ClonePath);

        if (!repoContext.HasDotNetFiles(out string? sourceDirectory, out IReadOnlyList<string>? solutions, out IReadOnlyList<string>? projects))
        {
            this._logger.LogInformation("No dotnet files found");
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, GitUtils.GetHeadRev(repoContext.Repository), cancellationToken: cancellationToken);

            return;
        }

        BuildSettings buildSettings = this._dotNetBuild.LoadBuildSettings(projects: projects);

        int totalUpdates = 0;

        foreach (PackageUpdate package in packages)
        {
            (bool updated, lastKnownGoodBuild) = await this.ProcessRepoOnePackageUpdateAsync(updateContext: updateContext,
                                                                                             repoContext: repoContext,
                                                                                             solutions: solutions,
                                                                                             sourceDirectory: sourceDirectory,
                                                                                             buildSettings: buildSettings,
                                                                                             dotNetSettings: updateContext.DotNetSettings,
                                                                                             package: package,
                                                                                             lastKnownGoodBuild: lastKnownGoodBuild,
                                                                                             cancellationToken: cancellationToken);

            if (updated)
            {
                ++totalUpdates;
            }
        }

        if (totalUpdates == 0)
        {
            // no updates in this run - so might be able to create a release
            await this._releaseGeneration.TryCreateNextPatchAsync(repoContext: repoContext,
                                                                  basePath: sourceDirectory,
                                                                  buildSettings: buildSettings,
                                                                  dotNetSettings: updateContext.DotNetSettings,
                                                                  solutions: solutions,
                                                                  packages: packages,
                                                                  cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<(bool updated, string? lastKnownGoodBuild)> ProcessRepoOnePackageUpdateAsync(UpdateContext updateContext,
                                                                                                         RepoContext repoContext,
                                                                                                         IReadOnlyList<string> solutions,
                                                                                                         string sourceDirectory,
                                                                                                         BuildSettings buildSettings,
                                                                                                         DotNetVersionSettings dotNetSettings,
                                                                                                         PackageUpdate package,
                                                                                                         string? lastKnownGoodBuild,
                                                                                                         CancellationToken cancellationToken)
    {
        bool updated = false;

        if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, GitUtils.GetHeadRev(repoContext.Repository)))
        {
            await this._solutionCheck.PreCheckAsync(solutions: solutions, dotNetSettings: dotNetSettings, logger: this._logger, cancellationToken: cancellationToken);

            await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);

            lastKnownGoodBuild = GitUtils.GetHeadRev(repoContext.Repository);
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: lastKnownGoodBuild, cancellationToken: cancellationToken);
        }

        IReadOnlyList<PackageVersion> updatesMade = await this.UpdatePackagesAsync(updateContext: updateContext, repoContext: repoContext, package: package, cancellationToken: cancellationToken);

        if (updatesMade.Count != 0)
        {
            updated = true;

            string? goodBuildCommit = await this.OnPackageUpdateAsync(updateContext: updateContext,
                                                                      repoContext: repoContext,
                                                                      solutions: solutions,
                                                                      sourceDirectory: sourceDirectory,
                                                                      buildSettings: buildSettings,
                                                                      updatesMade: updatesMade,
                                                                      package: package,
                                                                      cancellationToken: cancellationToken);

            return (updated, goodBuildCommit ?? lastKnownGoodBuild);
        }

        return (updated, lastKnownGoodBuild);
    }

    private async ValueTask<string?> OnPackageUpdateAsync(UpdateContext updateContext,
                                                          RepoContext repoContext,
                                                          IReadOnlyList<string> solutions,
                                                          string sourceDirectory,
                                                          BuildSettings buildSettings,
                                                          IReadOnlyList<PackageVersion> updatesMade,
                                                          PackageUpdate package,
                                                          CancellationToken cancellationToken)
    {
        bool ok = await this.PostUpdateCheckAsync(solutions: solutions,
                                                  sourceDirectory: sourceDirectory,
                                                  buildSettings: buildSettings,
                                                  dotNetSettings: updateContext.DotNetSettings,
                                                  cancellationToken: cancellationToken);

        NuGetVersion version = GetUpdateVersion(updatesMade);

        await CommitToRepositoryAsync(repoContext: repoContext, package: package, version.ToString(), builtOk: ok, cancellationToken: cancellationToken);

        if (ok)
        {
            string headRev = GitUtils.GetHeadRev(repoContext.Repository);

            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: headRev, cancellationToken: cancellationToken);

            return headRev;
        }

        await GitUtils.ResetToMasterAsync(repo: repoContext.Repository, upstream: UPSTREAM, cancellationToken: cancellationToken);

        return null;
    }

    private static NuGetVersion GetUpdateVersion(IReadOnlyList<PackageVersion> updatesMade)
    {
        return updatesMade.Select(x => x.Version)
                          .OrderByDescending(x => x.Version)
                          .First();
    }

    private static async ValueTask CommitToRepositoryAsync(RepoContext repoContext, PackageUpdate package, string version, bool builtOk, CancellationToken cancellationToken)
    {
        string branchPrefix = $"depends/update-{package.PackageId}/".ToLowerInvariant();
        string branchForUpdate = branchPrefix + version;

        if (builtOk)
        {
            await CommitChangeWithChangelogAsync(repoContext: repoContext, package: package, version: version, cancellationToken: cancellationToken);
            await GitUtils.PushAsync(repo: repoContext.Repository, cancellationToken: cancellationToken);
            await GitUtils.RemoveBranchesForPrefixAsync(repo: repoContext.Repository,
                                                        Guid.NewGuid()
                                                            .ToString(),
                                                        branchPrefix: branchPrefix,
                                                        upstream: UPSTREAM,
                                                        cancellationToken: cancellationToken);
        }
        else
        {
            if (GitUtils.DoesBranchExist(repo: repoContext.Repository, branchName: branchForUpdate))
            {
                // nothing to do - may already be a PR that's being worked on
                return;
            }

            GitUtils.CreateBranch(repo: repoContext.Repository, branchName: branchForUpdate);

            await CommitChangeWithChangelogAsync(repoContext: repoContext, package: package, version: version, cancellationToken: cancellationToken);
            await GitUtils.PushOriginAsync(repo: repoContext.Repository, branchName: branchForUpdate, upstream: UPSTREAM, cancellationToken: cancellationToken);
            await GitUtils.RemoveBranchesForPrefixAsync(repo: repoContext.Repository,
                                                        branchForUpdate: branchForUpdate,
                                                        branchPrefix: branchPrefix,
                                                        upstream: UPSTREAM,
                                                        cancellationToken: cancellationToken);
        }
    }

    private static async ValueTask CommitChangeWithChangelogAsync(RepoContext repoContext, PackageUpdate package, string version, CancellationToken cancellationToken)
    {
        await ChangeLogUpdater.RemoveEntryAsync(changeLogFileName: repoContext.ChangeLogFileName,
                                                type: CHANGELOG_ENTRYTYPE,
                                                $"Dependencies - Updated {package.PackageId} to ",
                                                cancellationToken: cancellationToken);
        await ChangeLogUpdater.AddEntryAsync(changeLogFileName: repoContext.ChangeLogFileName,
                                             type: CHANGELOG_ENTRYTYPE,
                                             $"Dependencies - Updated {package.PackageId} to {version}",
                                             cancellationToken: cancellationToken);

        await GitUtils.CommitAsync(repo: repoContext.Repository, $"[Dependencies] Updating {package.PackageId} ({package.PackageType}) to {version}", cancellationToken: cancellationToken);
    }

    private async ValueTask<bool> PostUpdateCheckAsync(IReadOnlyList<string> solutions,
                                                       string sourceDirectory,
                                                       BuildSettings buildSettings,
                                                       DotNetVersionSettings dotNetSettings,
                                                       CancellationToken cancellationToken)
    {
        bool ok = false;

        try
        {
            bool checkOk = await this._solutionCheck.PostCheckAsync(solutions: solutions, dotNetSettings: dotNetSettings, logger: this._logger, cancellationToken: cancellationToken);

            if (checkOk)
            {
                await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);

                ok = true;
            }
        }
        catch (DotNetBuildErrorException exception)
        {
            this._logger.LogError(exception: exception, message: "Build failed");
            ok = false;
        }

        return ok;
    }

    private async ValueTask<IReadOnlyList<PackageVersion>> UpdatePackagesAsync(UpdateContext updateContext, RepoContext repoContext, PackageUpdate package, CancellationToken cancellationToken)
    {
        this._logger.LogInformation($"* Updating {package.PackageId}...");
        PackageUpdateConfiguration config = BuildConfiguration(package);

        return await this._packageUpdater.UpdateAsync(repoContext.WorkingDirectory(), configuration: config, packageSources: updateContext.AdditionalSources, cancellationToken: cancellationToken);
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