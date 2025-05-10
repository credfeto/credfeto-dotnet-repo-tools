using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cocona;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

[SuppressMessage(
    category: "Microsoft.Performance",
    checkId: "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Instantiated by Cocona"
)]
[SuppressMessage(
    category: "ReSharper",
    checkId: "ClassNeverInstantiated.Global",
    Justification = "Instantiated by Cocona"
)]
internal sealed class Commands
{
    private readonly IBulkCodeCleanUp _bulkCodeCleanUp;
    private readonly IBulkPackageUpdater _bulkPackageUpdater;
    private readonly IBulkTemplateUpdater _bulkTemplateUpdater;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
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
        ILogger<Commands> logger
    )
    {
        this._gitRepositoryListLoader = gitRepositoryListLoader;
        this._bulkCodeCleanUp = bulkCodeCleanUp;
        this._bulkPackageUpdater = bulkPackageUpdater;
        this._bulkTemplateUpdater = bulkTemplateUpdater;
        this._logger = logger;
    }

    [Command("update-packages", Description = "Update all packages in all repositories")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Used by Cocona")]
    public async Task UpdatePackagesAsync(
        [Option(name: "repositories", ['r'], Description = "repos.lst file containing list of repositories")]
            string repositoriesFileName,
        [Option(name: "template", ['m'], Description = "Template repository to clone")] string templateRepository,
        [Option(name: "cache", ['c'], Description = "package cache file")] string? cacheFileName,
        [Option(name: "tracking", ['t'], Description = "folder where to write tracking.json file")]
            string trackingFileName,
        [Option(name: "packages", ['p'], Description = "Packages.json file to load")] string packagesFileName,
        [Option(name: "work", ['w'], Description = "folder where to clone repositories")] string workFolder,
        [Option(name: "release", ['l'], Description = "release.config file to load")] string releaseConfigFileName,
        [Option(name: "source", ['s'], Description = "Urls to additional NuGet feeds to load")]
            IEnumerable<string>? source
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: this._cancellationToken
        );

        this.Dump(repositories);

        IReadOnlyList<string> nugetSources = source?.ToArray() ?? [];
        await this._bulkPackageUpdater.BulkUpdateAsync(
            templateRepository: templateRepository,
            cacheFileName: cacheFileName,
            trackingFileName: trackingFileName,
            packagesFileName: packagesFileName,
            workFolder: workFolder,
            releaseConfigFileName: releaseConfigFileName,
            additionalNugetSources: nugetSources,
            repositories: repositories,
            cancellationToken: this._cancellationToken
        );

        this.Done();
    }

    private void Done()
    {
        this._logger.LogInformation("Done");
    }

    [Command("update-template", Description = "Update repos from template in all repositories")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Used by Cocona")]
    public async Task UpdateFromTemplateAsync(
        [Option(name: "repositories", ['r'], Description = "repos.lst file containing list of repositories")]
            string repositoriesFileName,
        [Option(name: "template", ['m'], Description = "Template repository to clone")] string templateRepository,
        [Option(name: "tracking", ['t'], Description = "folder where to write tracking.json file")]
            string trackingFileName,
        [Option(name: "packages", ['p'], Description = "Packages.json file to load")] string packagesFileName,
        [Option(name: "work", ['w'], Description = "folder where to clone repositories")] string workFolder,
        [Option(name: "release", ['l'], Description = "release.config file to load")] string releaseConfigFileName
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: this._cancellationToken
        );

        this.Dump(repositories);

        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: templateRepository,
            trackingFileName: trackingFileName,
            packagesFileName: packagesFileName,
            workFolder: workFolder,
            releaseConfigFileName: releaseConfigFileName,
            repositories: repositories,
            cancellationToken: this._cancellationToken
        );

        this.Done();
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

        return repositories is [] ? NoRepositories() : repositories;
    }

    [DoesNotReturn]
    private static IReadOnlyList<string> NoRepositories()
    {
        throw new InvalidOperationException("No Repositories found");
    }

    [Command("code-cleanup", Description = "Perform code cleanup in all repositories")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Used by Cocona")]
    public async Task CodeCleanupAsync(
        [Option(name: "repositories", ['r'], Description = "repos.lst file containing list of repositories")]
            string repositoriesFileName,
        [Option(name: "template", ['m'], Description = "Template repository to clone")] string templateRepository,
        [Option(name: "tracking", ['t'], Description = "folder where to write tracking.json file")]
            string trackingFileName,
        [Option(name: "packages", ['p'], Description = "Packages.json file to load")] string packagesFileName,
        [Option(name: "work", ['w'], Description = "folder where to clone repositories")] string workFolder,
        [Option(name: "release", ['l'], Description = "release.config file to load")] string releaseConfigFileName
    )
    {
        IReadOnlyList<string> repositories = await this.LoadRepositoriesAsync(
            repositoriesFileName: repositoriesFileName,
            templateRepository: templateRepository,
            cancellationToken: this._cancellationToken
        );

        this.Dump(repositories);

        await this._bulkCodeCleanUp.BulkUpdateAsync(
            templateRepository: templateRepository,
            trackingFileName: trackingFileName,
            packagesFileName: packagesFileName,
            workFolder: workFolder,
            releaseConfigFileName: releaseConfigFileName,
            repositories: repositories,
            cancellationToken: this._cancellationToken
        );

        this.Done();
    }

    private void Dump(IReadOnlyList<string> repositories)
    {
        foreach (string repo in repositories)
        {
            this._logger.LogDebug(repo);
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
