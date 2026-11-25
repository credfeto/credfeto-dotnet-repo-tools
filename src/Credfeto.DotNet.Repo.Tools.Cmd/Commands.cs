using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Cmd.Constants;
using Credfeto.DotNet.Repo.Tools.Cmd.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

public sealed class Commands
{
    private readonly IBulkCodeCleanUp _bulkCodeCleanUp;
    private readonly IBulkDependencyReducer _bulkDependencyReducer;
    private readonly IBulkPackageUpdater _bulkPackageUpdater;
    private readonly IBulkTemplateUpdater _bulkTemplateUpdater;
    private readonly IDependencyReducer _dependencyReducer;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGitRepositoryListLoader _gitRepositoryListLoader;
    private readonly ILogger<Commands> _logger;

    [SuppressMessage(
        category: "FunFair.CodeAnalysis",
        checkId: "FFS0023: Use ILogger rather than ILogger<T>",
        Justification = "Needed in this case"
    )]
    public Commands(
        IGitRepositoryListLoader gitRepositoryListLoader,
        IBulkCodeCleanUp bulkCodeCleanUp,
        IBulkPackageUpdater bulkPackageUpdater,
        IBulkTemplateUpdater bulkTemplateUpdater,
        IBulkDependencyReducer bulkDependencyReducer,
        IDependencyReducer dependencyReducer,
        IDotNetFilesDetector dotNetFilesDetector,
        IGitRepositoryFactory gitRepositoryFactory,
        ILogger<Commands> logger
    )
    {
        this._gitRepositoryListLoader = gitRepositoryListLoader;
        this._bulkCodeCleanUp = bulkCodeCleanUp;
        this._bulkPackageUpdater = bulkPackageUpdater;
        this._bulkTemplateUpdater = bulkTemplateUpdater;
        this._bulkDependencyReducer = bulkDependencyReducer;
        this._dependencyReducer = dependencyReducer;
        this._dotNetFilesDetector = dotNetFilesDetector;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._logger = logger;
    }

    [Command(CommandNames.UpdatePackages)]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0109: Consider adding an overload with a Span<T> or Memory<T>",
        Justification = "ConsoleAppFramework binds array options to string[]"
    )]
    public async Task<int> UpdatePackagesAsync(
        string repositories,
        string template,
        string tracking,
        string packages,
        string work,
        string release,
        string? cache = null,
        string[]? source = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await this.UpdatePackagesCoreAsync(
                repositoriesFileName: repositories,
                templateRepository: template,
                cacheFileName: cache,
                trackingFileName: tracking,
                packagesFileName: packages,
                workFolder: work,
                releaseConfigFileName: release,
                source: source,
                cancellationToken: cancellationToken
            );

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this._logger.LogCommandFailed(exception.Message, exception: exception);

            return ExitCodes.Error;
        }
    }

    private async Task UpdatePackagesCoreAsync(
        string repositoriesFileName,
        string templateRepository,
        string? cacheFileName,
        string trackingFileName,
        string packagesFileName,
        string workFolder,
        string releaseConfigFileName,
        IReadOnlyList<string>? source,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: cancellationToken
        );

        this.Dump(repositories);

        await this._bulkPackageUpdater.BulkUpdateAsync(
            templateRepository: templateRepository,
            cacheFileName: cacheFileName,
            trackingFileName: trackingFileName,
            packagesFileName: packagesFileName,
            workFolder: workFolder,
            releaseConfigFileName: releaseConfigFileName,
            additionalNugetSources: source ?? [],
            repositories: repositories,
            cancellationToken: cancellationToken
        );

        this.Done();
    }

    [Command(CommandNames.UpdateTemplate)]
    public async Task<int> UpdateFromTemplateAsync(
        string repositories,
        string template,
        string templateConfig,
        string tracking,
        string packages,
        string work,
        string release,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await this.UpdateFromTemplateCoreAsync(
                repositoriesFileName: repositories,
                templateRepository: template,
                templateConfigFileName: templateConfig,
                trackingFileName: tracking,
                packagesFileName: packages,
                workFolder: work,
                releaseConfigFileName: release,
                cancellationToken: cancellationToken
            );

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this._logger.LogCommandFailed(exception.Message, exception: exception);

            return ExitCodes.Error;
        }
    }

    private async Task UpdateFromTemplateCoreAsync(
        string repositoriesFileName,
        string templateRepository,
        string templateConfigFileName,
        string trackingFileName,
        string packagesFileName,
        string workFolder,
        string releaseConfigFileName,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: cancellationToken
        );

        this.Dump(repositories);

        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: templateRepository,
            trackingFileName: trackingFileName,
            packagesFileName: packagesFileName,
            workFolder: workFolder,
            templateConfigFileName: templateConfigFileName,
            releaseConfigFileName: releaseConfigFileName,
            repositories: repositories,
            cancellationToken: cancellationToken
        );

        this.Done();
    }

    [Command(CommandNames.CodeCleanup)]
    public async Task<int> CodeCleanupAsync(
        string repositories,
        string template,
        string tracking,
        string work,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await this.CodeCleanupCoreAsync(
                repositoriesFileName: repositories,
                templateRepository: template,
                trackingFileName: tracking,
                workFolder: work,
                cancellationToken: cancellationToken
            );

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this._logger.LogCommandFailed(exception.Message, exception: exception);

            return ExitCodes.Error;
        }
    }

    private async Task CodeCleanupCoreAsync(
        string repositoriesFileName,
        string templateRepository,
        string trackingFileName,
        string workFolder,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: cancellationToken
        );

        this.Dump(repositories);

        await this._bulkCodeCleanUp.BulkUpdateAsync(
            templateRepository: templateRepository,
            trackingFileName: trackingFileName,
            workFolder: workFolder,
            repositories: repositories,
            cancellationToken: cancellationToken
        );

        this.Done();
    }

    [Command(CommandNames.ReduceDependencies)]
    public async Task<int> ReduceDependenciesAsync(
        string repositories,
        string template,
        string tracking,
        string work,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await this.ReduceDependenciesCoreAsync(
                repositoriesFileName: repositories,
                templateRepository: template,
                trackingFileName: tracking,
                workFolder: work,
                cancellationToken: cancellationToken
            );

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this._logger.LogCommandFailed(exception.Message, exception: exception);

            return ExitCodes.Error;
        }
    }

    private async Task ReduceDependenciesCoreAsync(
        string repositoriesFileName,
        string templateRepository,
        string trackingFileName,
        string workFolder,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: cancellationToken
        );

        this.Dump(repositories);

        await this._bulkDependencyReducer.BulkUpdateAsync(
            templateRepository: templateRepository,
            trackingFileName: trackingFileName,
            workFolder: workFolder,
            repositories: repositories,
            cancellationToken: cancellationToken
        );

        this.Done();
    }

    [Command(CommandNames.CheckDependencies)]
    public async Task<int> CheckDependenciesAsync(string sourceFolder, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.CheckDependenciesCoreAsync(workFolder: sourceFolder, cancellationToken: cancellationToken);

            return ExitCodes.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this._logger.LogCommandFailed(exception.Message, exception: exception);

            return ExitCodes.Error;
        }
    }

    private async Task CheckDependenciesCoreAsync(string workFolder, CancellationToken cancellationToken)
    {
        DotNetFiles dotNetFiles = await this._dotNetFilesDetector.FindAsync(
            baseFolder: workFolder,
            cancellationToken: cancellationToken
        );

        using (IGitRepository repository = this._gitRepositoryFactory.OpenExisting(workFolder))
        {
            await this._dependencyReducer.CheckReferencesAsync(
                dotNetFiles: dotNetFiles,
                CreateCommittingReferenceConfig(repository),
                cancellationToken: cancellationToken
            );
        }

        this.Done();
    }

    private void Done()
    {
        this._logger.LogCompleted();
    }

    private async ValueTask<IReadOnlyList<string>> LoadRepositoriesAsync(
        string repositoriesFileName,
        string templateRepository,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> source = await this._gitRepositoryListLoader.LoadAsync(
            path: repositoriesFileName,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<string> repositories = ExcludeTemplateRepo(
            repositories: source,
            templateRepository: templateRepository
        );

        if (repositories is [])
        {
            throw new InvalidOperationException("No Repositories found");
        }

        return repositories;
    }

    private static ReferenceConfig CreateCommittingReferenceConfig(IGitRepository repository)
    {
        return new(CommitAsync);

        ValueTask CommitAsync(string projectFileName, string message, CancellationToken cancellationToken)
        {
            return repository.CommitNamedAsync(
                message: message,
                [Path.GetFullPath(projectFileName)],
                cancellationToken: cancellationToken
            );
        }
    }

    [SuppressMessage("codecracker.CSharp", "CC0091: Make Static", Justification = "Needed For Logging")]
    private void Dump(IReadOnlyList<string> repositories)
    {
        foreach (string repo in repositories)
        {
            this._logger.LogRepo(repo);
        }
    }

    private static IReadOnlyList<string> ExcludeTemplateRepo(
        IReadOnlyList<string> repositories,
        string templateRepository
    )
    {
        return
        [
            .. repositories.Where(repositoryUrl =>
                !StringComparer.OrdinalIgnoreCase.Equals(x: templateRepository, y: repositoryUrl)
            ),
        ];
    }
}
