using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Package;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services;

public sealed class SinglePackageUpdater : ISinglePackageUpdater
{
    private const string CHANGELOG_ENTRY_TYPE = "Changed";
    private readonly IDotNetBuild _dotNetBuild;

    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;
    private readonly ILogger<SinglePackageUpdater> _logger;
    private readonly IPackageUpdateConfigurationBuilder _packageUpdateConfigurationBuilder;
    private readonly IPackageUpdater _packageUpdater;
    private readonly ITrackingCache _trackingCache;
    private readonly ITrackingHashGenerator _trackingHashGenerator;

    public SinglePackageUpdater(IDotNetSolutionCheck dotNetSolutionCheck,
                                IDotNetBuild dotNetBuild,
                                ITrackingCache trackingCache,
                                ITrackingHashGenerator trackingHashGenerator,
                                IPackageUpdater packageUpdater,
                                IPackageUpdateConfigurationBuilder packageUpdateConfigurationBuilder,
                                ILogger<SinglePackageUpdater> logger)
    {
        this._dotNetSolutionCheck = dotNetSolutionCheck;
        this._dotNetBuild = dotNetBuild;
        this._trackingCache = trackingCache;
        this._trackingHashGenerator = trackingHashGenerator;
        this._packageUpdater = packageUpdater;
        this._packageUpdateConfigurationBuilder = packageUpdateConfigurationBuilder;
        this._logger = logger;
    }

    public async ValueTask<bool> UpdateAsync(PackageUpdateContext updateContext,
                                             RepoContext repoContext,
                                             IReadOnlyList<string> solutions,
                                             string sourceDirectory,
                                             BuildSettings buildSettings,
                                             DotNetVersionSettings dotNetSettings,
                                             PackageUpdate package,
                                             CancellationToken cancellationToken)
    {
        await this.RequireSolutionBuildsBeforeUpdateAsync(updateContext: updateContext,
                                                          repoContext: repoContext,
                                                          solutions: solutions,
                                                          sourceDirectory: sourceDirectory,
                                                          buildSettings: buildSettings,
                                                          dotNetSettings: dotNetSettings,
                                                          cancellationToken: cancellationToken);

        IReadOnlyList<PackageVersion> updatesMade = await this.UpdatePackagesAsync(updateContext: updateContext, repoContext: repoContext, package: package, cancellationToken: cancellationToken);

        if (updatesMade is [])
        {
            await RemoveExistingBranchesForPackageAsync(repoContext: repoContext, package: package, cancellationToken: cancellationToken);

            return false;
        }

        try
        {
            await this.OnPackageUpdateAsync(updateContext: updateContext,
                                            repoContext: repoContext,
                                            solutions: solutions,
                                            sourceDirectory: sourceDirectory,
                                            buildSettings: buildSettings,
                                            repositoryDotNetVersionSettings: dotNetSettings,
                                            updatesMade: updatesMade,
                                            package: package,
                                            cancellationToken: cancellationToken);

            return true;
        }
        finally
        {
            this._logger.LogResettingToDefault(repoContext);
            await repoContext.Repository.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask RequireSolutionBuildsBeforeUpdateAsync(PackageUpdateContext updateContext,
                                                                   RepoContext repoContext,
                                                                   IReadOnlyList<string> solutions,
                                                                   string sourceDirectory,
                                                                   BuildSettings buildSettings,
                                                                   DotNetVersionSettings dotNetSettings,
                                                                   CancellationToken cancellationToken)
    {
        string? lastKnownGoodBuild = this._trackingCache.Get(repoContext.ClonePath);

        if (lastKnownGoodBuild is not null && StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, y: repoContext.Repository.HeadRev))
        {
            // content of last build was successful
            return;
        }

        BuildOverride buildOverride = new(PreRelease: true);
        await this._dotNetSolutionCheck.PreCheckAsync(solutions: solutions,
                                                      repositoryDotNetSettings: dotNetSettings,
                                                      templateDotNetSettings: updateContext.DotNetSettings,
                                                      cancellationToken: cancellationToken);

        await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

        await this.UpdateTrackingHashAsync(repoContext: repoContext, updateContext: updateContext, cancellationToken: cancellationToken);
    }

    private async ValueTask UpdateTrackingHashAsync(RepoContext repoContext, PackageUpdateContext updateContext, CancellationToken cancellationToken)
    {
        string hash = await this._trackingHashGenerator.GenerateTrackingHashAsync(repoContext: repoContext, cancellationToken: cancellationToken);
        await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: hash, cancellationToken: cancellationToken);
    }

    private static ValueTask RemoveExistingBranchesForPackageAsync(in RepoContext repoContext, PackageUpdate package, in CancellationToken cancellationToken)
    {
        string branchPrefix = GetBranchPrefixForPackage(package);
        string invalidUpdateBranch = BranchNaming.BuildInvalidUpdateBranch(branchPrefix);

        return repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: invalidUpdateBranch,
                                                                   branchPrefix: branchPrefix,
                                                                   upstream: GitConstants.Upstream,
                                                                   cancellationToken: cancellationToken);
    }

    private async ValueTask OnPackageUpdateAsync(PackageUpdateContext updateContext,
                                                 RepoContext repoContext,
                                                 IReadOnlyList<string> solutions,
                                                 string sourceDirectory,
                                                 BuildSettings buildSettings,
                                                 DotNetVersionSettings repositoryDotNetVersionSettings,
                                                 IReadOnlyList<PackageVersion> updatesMade,
                                                 PackageUpdate package,
                                                 CancellationToken cancellationToken)
    {
        bool ok = await this.PostUpdateCheckAsync(solutions: solutions,
                                                  sourceDirectory: sourceDirectory,
                                                  buildSettings: buildSettings,
                                                  repositoryDotNetVersionSettings: repositoryDotNetVersionSettings,
                                                  templateDotNetSettings: updateContext.DotNetSettings,
                                                  cancellationToken: cancellationToken);

        NuGetVersion version = GetUpdateVersion(updatesMade);

        await this.CommitToRepositoryAsync(repoContext: repoContext, package: package, version.ToString(), builtOk: ok, cancellationToken: cancellationToken);

        if (ok)
        {
            await this.UpdateTrackingHashAsync(repoContext: repoContext, updateContext: updateContext, cancellationToken: cancellationToken);
        }
    }

    private static NuGetVersion GetUpdateVersion(IReadOnlyList<PackageVersion> updatesMade)
    {
        return updatesMade.Select(x => x.Version)
                          .OrderByDescending(x => x.Version)
                          .First();
    }

    private async ValueTask CommitToRepositoryAsync(RepoContext repoContext, PackageUpdate package, string version, bool builtOk, CancellationToken cancellationToken)
    {
        try
        {
            string branchPrefix = GetBranchPrefixForPackage(package);

            if (builtOk)
            {
                string invalidUpdateBranch = BranchNaming.BuildInvalidUpdateBranch(branchPrefix);

                await this.CommitDefaultBranchToRepositoryAsync(repoContext: repoContext,
                                                                package: package,
                                                                version: version,
                                                                invalidUpdateBranch: invalidUpdateBranch,
                                                                branchPrefix: branchPrefix,
                                                                cancellationToken: cancellationToken);
            }
            else
            {
                string branchForUpdate = BranchNaming.BuildBranchForVersion(branchPrefix: branchPrefix, version: version);

                await this.CommitToNamedBranchAsync(repoContext: repoContext,
                                                    package: package,
                                                    version: version,
                                                    branchForUpdate: branchForUpdate,
                                                    branchPrefix: branchPrefix,
                                                    cancellationToken: cancellationToken);
            }
        }
        finally
        {
            this._logger.LogResettingToDefault(repoContext);
            await repoContext.Repository.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask CommitToNamedBranchAsync(RepoContext repoContext, PackageUpdate package, string version, string branchForUpdate, string branchPrefix, CancellationToken cancellationToken)
    {
        if (repoContext.Repository.DoesBranchExist(branchName: branchForUpdate))
        {
            // nothing to do - may already be a PR that's being worked on
            this._logger.LogSkippingPackageCommit(repoContext: repoContext, branch: branchForUpdate, packageId: package.PackageId, version: version);

            return;
        }

        this._logger.LogCommittingToNamedBranch(repoContext: repoContext, branch: branchForUpdate, packageId: package.PackageId, version: version);
        await repoContext.Repository.CreateBranchAsync(branchName: branchForUpdate, cancellationToken: cancellationToken);

        await CommitChangeWithChangelogAsync(repoContext: repoContext, package: package, version: version, cancellationToken: cancellationToken);
        await repoContext.Repository.PushOriginAsync(branchName: branchForUpdate, upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: branchForUpdate, branchPrefix: branchPrefix, upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        await repoContext.Repository.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
    }

    private async ValueTask CommitDefaultBranchToRepositoryAsync(RepoContext repoContext,
                                                                 PackageUpdate package,
                                                                 string version,
                                                                 string invalidUpdateBranch,
                                                                 string branchPrefix,
                                                                 CancellationToken cancellationToken)
    {
        this._logger.LogCommittingToDefault(repoContext: repoContext, packageId: package.PackageId, version: version);
        await CommitChangeWithChangelogAsync(repoContext: repoContext, package: package, version: version, cancellationToken: cancellationToken);
        await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);
        await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: invalidUpdateBranch,
                                                                  branchPrefix: branchPrefix,
                                                                  upstream: GitConstants.Upstream,
                                                                  cancellationToken: cancellationToken);
    }

    private static string GetBranchPrefixForPackage(PackageUpdate package)
    {
        return $"depends/update-{package.PackageId}/".ToLowerInvariant();
    }

    private static async ValueTask CommitChangeWithChangelogAsync(RepoContext repoContext, PackageUpdate package, string version, CancellationToken cancellationToken)
    {
        await ChangeLogUpdater.RemoveEntryAsync(changeLogFileName: repoContext.ChangeLogFileName,
                                                type: CHANGELOG_ENTRY_TYPE,
                                                $"Dependencies - Updated {package.PackageId} to ",
                                                cancellationToken: cancellationToken);
        await ChangeLogUpdater.AddEntryAsync(changeLogFileName: repoContext.ChangeLogFileName,
                                             type: CHANGELOG_ENTRY_TYPE,
                                             $"Dependencies - Updated {package.PackageId} to {version}",
                                             cancellationToken: cancellationToken);

        await repoContext.Repository.CommitAsync($"[Dependencies] Updating {package.PackageId} ({package.PackageType}) to {version}", cancellationToken: cancellationToken);
    }

    private async ValueTask<bool> PostUpdateCheckAsync(IReadOnlyList<string> solutions,
                                                       string sourceDirectory,
                                                       BuildSettings buildSettings,
                                                       DotNetVersionSettings repositoryDotNetVersionSettings,
                                                       DotNetVersionSettings templateDotNetSettings,
                                                       CancellationToken cancellationToken)
    {
        try
        {
            bool checkOk = await this._dotNetSolutionCheck.PostCheckAsync(solutions: solutions,
                                                                          repositoryDotNetSettings: repositoryDotNetVersionSettings,
                                                                          templateDotNetSettings: templateDotNetSettings,
                                                                          cancellationToken: cancellationToken);

            if (checkOk)
            {
                BuildOverride buildOverride = new(PreRelease: true);
                await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

                return true;
            }
        }
        catch (DotNetBuildErrorException exception)
        {
            this._logger.LogBuildFailedAfterPackageUpdate(exception: exception);
        }

        return false;
    }

    private ValueTask<IReadOnlyList<PackageVersion>> UpdatePackagesAsync(in PackageUpdateContext updateContext,
                                                                         in RepoContext repoContext,
                                                                         PackageUpdate package,
                                                                         in CancellationToken cancellationToken)
    {
        this._logger.LogUpdatingPackageId(package.PackageId);
        PackageUpdateConfiguration config = this._packageUpdateConfigurationBuilder.Build(package);

        return this._packageUpdater.UpdateAsync(basePath: repoContext.WorkingDirectory, configuration: config, packageSources: updateContext.AdditionalSources, cancellationToken: cancellationToken);
    }
}