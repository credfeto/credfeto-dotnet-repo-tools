using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class BulkDependencyReducer : IBulkDependencyReducer
{
    private readonly IDependencyReducer _dependencyReducer;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly ILogger<BulkDependencyReducer> _logger;
    private readonly ITrackingCache _trackingCache;

    public BulkDependencyReducer(ITrackingCache trackingCache,
                                 IGlobalJson globalJson,
                                 IDotNetVersion dotNetVersion,
                                 IGitRepositoryFactory gitRepositoryFactory,
                                 IDependencyReducer dependencyReducer,
                                 ILogger<BulkDependencyReducer> logger)
    {
        this._trackingCache = trackingCache;
        this._globalJson = globalJson;
        this._dotNetVersion = dotNetVersion;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._dependencyReducer = dependencyReducer;
        this._logger = logger;
    }

    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> additionalNugetSources,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        await this.LoadTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);

        using (IGitRepository templateRepo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: workFolder, repoUrl: templateRepository, cancellationToken: cancellationToken))
        {
            DependencyReductionUpdateContext updateContext = await this.BuildUpdateContextAsync(templateRepo: templateRepo,
                                                                                                workFolder: workFolder,
                                                                                                trackingFileName: trackingFileName,
                                                                                                additionalNugetSources: additionalNugetSources,
                                                                                                cancellationToken: cancellationToken);

            try
            {
                await this.UpdateRepositoriesAsync(updateContext: updateContext, repositories: repositories, cancellationToken: cancellationToken);
            }
            finally
            {
                await this.SaveTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask UpdateRepositoriesAsync(DependencyReductionUpdateContext updateContext, IReadOnlyList<string> repositories, CancellationToken cancellationToken)
    {
        foreach (string repo in repositories)
        {
            try
            {
                await this.UpdateRepositoryAsync(updateContext: updateContext, repo: repo, cancellationToken: cancellationToken);
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

    private async Task UpdateRepositoryAsync(DependencyReductionUpdateContext updateContext, string repo, CancellationToken cancellationToken)
    {
        this._logger.LogProcessingRepo(repo);

        using (IGitRepository repository = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            if (!ChangeLogDetector.TryFindChangeLog(repository: repository.Active, out string? changeLogFileName))
            {
                this._logger.LogNoChangelogFound();

                return;
            }

            RepoContext repoContext = new(Repository: repository, ChangeLogFileName: changeLogFileName);

            await this.ProcessRepoUpdatesAsync(repoContext: repoContext, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessRepoUpdatesAsync(RepoContext repoContext, CancellationToken cancellationToken)
    {
        try
        {
            ReferenceConfig config = new(CommitAsync);

            bool result = await this._dependencyReducer.CheckReferencesAsync(sourceDirectory: repoContext.WorkingDirectory, config: config, cancellationToken: cancellationToken);

            this._logger.LogWorkingChangeStatus(repo: repoContext.ClonePath, changes: result);
        }
        finally
        {
            await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        }

        async ValueTask CommitAsync(string projectFileName, string message, CancellationToken ct)
        {
            await repoContext.Repository.CommitNamedAsync(message: message, [projectFileName], cancellationToken: ct);
            await repoContext.Repository.PushAsync(ct);
            await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: ct);
        }
    }

    private async ValueTask<DependencyReductionUpdateContext> BuildUpdateContextAsync(IGitRepository templateRepo,
                                                                                      string workFolder,
                                                                                      string trackingFileName,
                                                                                      IReadOnlyList<string> additionalNugetSources,
                                                                                      CancellationToken cancellationToken)
    {
        DotNetVersionSettings dotNetSettings = await this._globalJson.LoadGlobalJsonAsync(baseFolder: templateRepo.WorkingDirectory, cancellationToken: cancellationToken);

        IReadOnlyList<Version> installedDotNetSdks = await this._dotNetVersion.GetInstalledSdksAsync(cancellationToken);

        if (dotNetSettings.SdkVersion is not null && Version.TryParse(input: dotNetSettings.SdkVersion, out Version? sdkVersion))
        {
            if (!installedDotNetSdks.Contains(sdkVersion))
            {
                this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedDotNetSdks);

                throw new DotNetBuildErrorException("SDK version specified in global.json is not installed");
            }
        }

        return new(WorkFolder: workFolder, TrackingFileName: trackingFileName, AdditionalSources: additionalNugetSources, DotNetSettings: dotNetSettings);
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
}