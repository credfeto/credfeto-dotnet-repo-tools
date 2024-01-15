using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class BulkTemplateUpdater : IBulkTemplateUpdater
{
    private const string CHANGELOG_ENTRY_TYPE = "Changed";
    private readonly IBulkPackageConfigLoader _bulkPackageConfigLoader;
    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly ILogger<BulkTemplateUpdater> _logger;
    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly IReleaseGeneration _releaseGeneration;
    private readonly ITrackingCache _trackingCache;

    public BulkTemplateUpdater(ITrackingCache trackingCache,
                               IGlobalJson globalJson,
                               IDotNetVersion dotNetVersion,
                               IDotNetSolutionCheck dotNetSolutionCheck,
                               IDotNetBuild dotNetBuild,
                               IReleaseConfigLoader releaseConfigLoader,
                               IReleaseGeneration releaseGeneration,
                               IGitRepositoryFactory gitRepositoryFactory,
                               IBulkPackageConfigLoader bulkPackageConfigLoader,
                               ILogger<BulkTemplateUpdater> logger)
    {
        this._trackingCache = trackingCache;
        this._globalJson = globalJson;
        this._dotNetVersion = dotNetVersion;
        this._dotNetSolutionCheck = dotNetSolutionCheck;
        this._dotNetBuild = dotNetBuild;
        this._releaseConfigLoader = releaseConfigLoader;
        this._releaseGeneration = releaseGeneration;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._bulkPackageConfigLoader = bulkPackageConfigLoader;
        this._logger = logger;
    }

    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        await this.LoadTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);

        IReadOnlyList<PackageUpdate> packages = await this._bulkPackageConfigLoader.LoadAsync(path: packagesFileName, cancellationToken: cancellationToken);

        using (IGitRepository templateRepo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: workFolder, repoUrl: templateRepository, cancellationToken: cancellationToken))
        {
            TemplateUpdateContext updateContext = await this.BuildUpdateContextAsync(templateRepo: templateRepo,
                                                                                     workFolder: workFolder,
                                                                                     trackingFileName: trackingFileName,
                                                                                     releaseConfigFileName: releaseConfigFileName,
                                                                                     cancellationToken: cancellationToken);

            try
            {
                await this.UpdateRepositoriesAsync(updateContext: updateContext, repositories: repositories, packages: packages, cancellationToken: cancellationToken);
            }
            finally
            {
                await this.SaveTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask UpdateRepositoriesAsync(TemplateUpdateContext updateContext, IReadOnlyList<string> repositories, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        try
        {
            foreach (string repo in repositories)
            {
                try
                {
                    await this.UpdateRepositoryAsync(updateContext: updateContext, packages: packages, cancellationToken: cancellationToken, repo: repo);
                }
                catch (SolutionCheckFailedException exception)
                {
                    this._logger.LogSolutionCheckFailed(exception: exception);
                }
                catch (DotNetBuildErrorException exception)
                {
                    this._logger.LogBuildFailedOnRepoCheck(exception: exception);
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(updateContext.TrackingFileName))
                    {
                        await this._trackingCache.SaveAsync(fileName: updateContext.TrackingFileName, cancellationToken: cancellationToken);
                    }
                }
            }
        }
        catch (ReleaseCreatedException exception)
        {
            this._logger.LogReleaseCreatedAbortingRun(exception: exception);
            this._logger.LogInformation(exception.Message);
        }
    }

    private async ValueTask UpdateRepositoryAsync(TemplateUpdateContext updateContext, IReadOnlyList<PackageUpdate> packages, string repo, CancellationToken cancellationToken)
    {
        this._logger.LogProcessingRepo(repo);

        using (IGitRepository repository = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            if (!ChangeLogDetector.TryFindChangeLog(repository: repository.Active, out string? changeLogFileName))
            {
                this._logger.LogNoChangelogFound();
                await this._trackingCache.UpdateTrackingAsync(new(Repository: repository, ChangeLogFileName: "?"),
                                                              updateContext: updateContext,
                                                              value: repository.HeadRev,
                                                              cancellationToken: cancellationToken);

                return;
            }

            RepoContext repoContext = new(Repository: repository, ChangeLogFileName: changeLogFileName);

            await this.ProcessRepoUpdatesAsync(updateContext: updateContext, repoContext: repoContext, packages: packages, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask ProcessRepoUpdatesAsync(TemplateUpdateContext updateContext, RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        string? lastKnownGoodBuild = this._trackingCache.Get(repoContext.ClonePath);

        int totalUpdates = await this.UpdateStandardFilesAsync(updateContext: updateContext, repoContext: repoContext, cancellationToken: cancellationToken);

        // TODO: Update non C# files

        if (repoContext.HasDotNetFiles(out string? sourceDirectory, out IReadOnlyList<string>? solutions, out IReadOnlyList<string>? projects))
        {
            await this.UpdateDotNetAsync(updateContext: updateContext,
                                         repoContext: repoContext,
                                         packages: packages,
                                         projects: projects,
                                         lastKnownGoodBuild: lastKnownGoodBuild,
                                         solutions: solutions,
                                         sourceDirectory: sourceDirectory,
                                         totalUpdates: totalUpdates,
                                         cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogNoDotNetFilesFound();
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: repoContext.Repository.HeadRev, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<int> UpdateStandardFilesAsync(TemplateUpdateContext updateContext, RepoContext repoContext, CancellationToken cancellationToken)
    {
        this._logger.LogInformation($"{updateContext.TemplateFolder} is up to date");
        this._logger.LogInformation($"{repoContext.WorkingDirectory} is up to date");

        await Task.Delay(millisecondsDelay: 0, cancellationToken: cancellationToken);

        // TODO: Implement

        return 0;
    }

    private async ValueTask UpdateDotNetAsync(TemplateUpdateContext updateContext,
                                              RepoContext repoContext,
                                              IReadOnlyList<PackageUpdate> packages,
                                              IReadOnlyList<string> projects,
                                              string? lastKnownGoodBuild,
                                              IReadOnlyList<string> solutions,
                                              string sourceDirectory,
                                              int totalUpdates,
                                              CancellationToken cancellationToken)
    {
        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(projects: projects, cancellationToken: cancellationToken);

        if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, y: repoContext.Repository.HeadRev))
        {
            await this._dotNetSolutionCheck.PreCheckAsync(solutions: solutions, dotNetSettings: updateContext.DotNetSettings, cancellationToken: cancellationToken);

            await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);

            lastKnownGoodBuild = repoContext.Repository.HeadRev;
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: lastKnownGoodBuild, cancellationToken: cancellationToken);
        }

        bool changed = await this.UpdateGlobalJsonAsync(repoContext: repoContext,
                                                        updateContext: updateContext,
                                                        sourceDirectory: sourceDirectory,
                                                        solutions: solutions,
                                                        buildSettings: buildSettings,
                                                        cancellationToken: cancellationToken);

        if (changed)
        {
            ++totalUpdates;
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
                                                                  releaseConfig: updateContext.ReleaseConfig,
                                                                  cancellationToken: cancellationToken);
        }
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "To be refactored")]
    private async ValueTask<bool> UpdateGlobalJsonAsync(RepoContext repoContext,
                                                        TemplateUpdateContext updateContext,
                                                        string sourceDirectory,
                                                        IReadOnlyList<string> solutions,
                                                        BuildSettings buildSettings,
                                                        CancellationToken cancellationToken)
    {
        string templateGlobalJsonFileName = Path.Combine(path1: updateContext.TemplateFolder, path2: "src", path3: "global.json");
        string targetGlobalJsonFileName = Path.Combine(path1: sourceDirectory, path2: "global.json");

        const string messagePrefix = "SDK - Updated DotNet SDK to ";
        string message = messagePrefix + updateContext.DotNetSettings.SdkVersion;

        const string branchPrefix = "depends/dotnet/sdk/";
        string branchName = branchPrefix + updateContext.DotNetSettings.SdkVersion;

        bool branchCreated = false;

        try
        {
            bool changed = await this.UpdateFileAsync(repoContext: repoContext,
                                                      templateGlobalJsonFileName: templateGlobalJsonFileName,
                                                      targetGlobalJsonFileName: targetGlobalJsonFileName,
                                                      commitMessage: message,
                                                      changelogUpdate: ChangelogUpdate,
                                                      cancellationToken: cancellationToken);

            if (changed)
            {
                if (branchCreated)
                {
                    await repoContext.Repository.PushOriginAsync(branchName: branchName, upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
                }

                string lastKnownGoodBuild = repoContext.Repository.HeadRev;
                await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: lastKnownGoodBuild, cancellationToken: cancellationToken);

                await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: branchName,
                                                                          branchPrefix: branchPrefix,
                                                                          upstream: GitConstants.Upstream,
                                                                          cancellationToken: cancellationToken);
            }

            return changed;
        }
        catch (BranchAlreadyExistsException)
        {
            return false;
        }
        finally
        {
            await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        }

        async ValueTask ChangelogUpdate(CancellationToken token)
        {
            await ChangeLogUpdater.RemoveEntryAsync(changeLogFileName: repoContext.ChangeLogFileName, type: CHANGELOG_ENTRY_TYPE, message: messagePrefix, cancellationToken: token);
            await ChangeLogUpdater.AddEntryAsync(changeLogFileName: repoContext.ChangeLogFileName, type: CHANGELOG_ENTRY_TYPE, message: message, cancellationToken: token);

            bool ok = await this.CheckBuildAsync(updateContext: updateContext,
                                                 sourceDirectory: sourceDirectory,
                                                 solutions: solutions,
                                                 buildSettings: buildSettings,
                                                 cancellationToken: cancellationToken);

            if (!ok)
            {
                if (repoContext.Repository.DoesBranchExist(branchName))
                {
                    throw new BranchAlreadyExistsException(branchName);
                }

                await repoContext.Repository.CreateBranchAsync(branchName: branchName, cancellationToken: cancellationToken);

                branchCreated = true;
            }
            else
            {
                await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: branchName,
                                                                          branchPrefix: branchPrefix,
                                                                          upstream: GitConstants.Upstream,
                                                                          cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<bool> CheckBuildAsync(TemplateUpdateContext updateContext,
                                             string sourceDirectory,
                                             IReadOnlyList<string> solutions,
                                             BuildSettings buildSettings,
                                             CancellationToken cancellationToken)
    {
        try
        {
            await this._dotNetSolutionCheck.PreCheckAsync(solutions: solutions, dotNetSettings: updateContext.DotNetSettings, cancellationToken: cancellationToken);

            await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);

            return true;
        }
        catch (SolutionCheckFailedException)
        {
            return false;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private async ValueTask<bool> UpdateFileAsync(RepoContext repoContext,
                                                  string templateGlobalJsonFileName,
                                                  string targetGlobalJsonFileName,
                                                  string commitMessage,
                                                  Func<CancellationToken, ValueTask> changelogUpdate,
                                                  CancellationToken cancellationToken)
    {
        Difference diff = await IsSameContentAsync(sourceFileName: templateGlobalJsonFileName, targetFileName: targetGlobalJsonFileName, cancellationToken: cancellationToken);

        return diff switch
        {
            Difference.TARGET_MISSING or Difference.DIFFERENT => await ReplaceFileAsync(repoContext: repoContext,
                                                                                        templateGlobalJsonFileName: templateGlobalJsonFileName,
                                                                                        targetGlobalJsonFileName: targetGlobalJsonFileName,
                                                                                        commitMessage: commitMessage,
                                                                                        changelogUpdate: changelogUpdate,
                                                                                        cancellationToken: cancellationToken),
            _ => AlreadyUpToDate()
        };

        bool AlreadyUpToDate()
        {
            this._logger.LogDebug($"{targetGlobalJsonFileName} is up to date");

            return false;
        }
    }

    private static async ValueTask<bool> ReplaceFileAsync(RepoContext repoContext,
                                                          string templateGlobalJsonFileName,
                                                          string targetGlobalJsonFileName,
                                                          string commitMessage,
                                                          Func<CancellationToken, ValueTask> changelogUpdate,
                                                          CancellationToken cancellationToken)
    {
        File.Copy(sourceFileName: templateGlobalJsonFileName, destFileName: targetGlobalJsonFileName, overwrite: true);

        await changelogUpdate(cancellationToken);
        await repoContext.Repository.CommitAsync(message: commitMessage, cancellationToken: cancellationToken);

        return true;
    }

    private async ValueTask<TemplateUpdateContext> BuildUpdateContextAsync(IGitRepository templateRepo,
                                                                           string workFolder,
                                                                           string trackingFileName,
                                                                           string releaseConfigFileName,
                                                                           CancellationToken cancellationToken)
    {
        DotNetVersionSettings dotNetSettings = await this._globalJson.LoadGlobalJsonAsync(baseFolder: templateRepo.WorkingDirectory, cancellationToken: cancellationToken);

        IReadOnlyList<Version> installedDotNetSdks = await this._dotNetVersion.GetInstalledSdksAsync(cancellationToken);

        if (dotNetSettings.SdkVersion is not null && Version.TryParse(input: dotNetSettings.SdkVersion, out Version? sdkVersion))
        {
            if (!installedDotNetSdks.Contains(sdkVersion))
            {
                this._logger.LogError($"SDK {sdkVersion} was requested, but not installed.  Currently installed SDKS: {string.Join(separator: ", ", values: installedDotNetSdks)}");

                throw new DotNetBuildErrorException("SDK version specified in global.json is not installed");
            }
        }

        ReleaseConfig releaseConfig = await this._releaseConfigLoader.LoadAsync(path: releaseConfigFileName, cancellationToken: cancellationToken);

        return new(WorkFolder: workFolder, TemplateFolder: templateRepo.WorkingDirectory, TrackingFileName: trackingFileName, DotNetSettings: dotNetSettings, ReleaseConfig: releaseConfig);
    }

    private ValueTask LoadTrackingCacheAsync(string? trackingFile, in CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingFile))
        {
            return ValueTask.CompletedTask;
        }

        if (!File.Exists(trackingFile))
        {
            return ValueTask.CompletedTask;
        }

        return this._trackingCache.LoadAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }

    private ValueTask SaveTrackingCacheAsync(string? trackingFile, in CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(trackingFile)
            ? ValueTask.CompletedTask
            : this._trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }

    private static async ValueTask<Difference> IsSameContentAsync(string sourceFileName, string targetFileName, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceFileName))
        {
            return Difference.SOURCE_MISSING;
        }

        if (!File.Exists(targetFileName))
        {
            return Difference.SOURCE_MISSING;
        }

        byte[] sourceBytes = await File.ReadAllBytesAsync(path: sourceFileName, cancellationToken: cancellationToken);
        byte[] targetBytes = await File.ReadAllBytesAsync(path: targetFileName, cancellationToken: cancellationToken);

        if (sourceBytes.SequenceEqual(targetBytes))
        {
            return Difference.SAME;
        }

        return Difference.DIFFERENT;
    }

    private enum Difference
    {
        SAME,
        SOURCE_MISSING,
        TARGET_MISSING,
        DIFFERENT
    }
}