using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cocona;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Cmd;

[SuppressMessage(category: "Microsoft.Performance", checkId: "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by Cocona")]
[SuppressMessage(category: "ReSharper", checkId: "ClassNeverInstantiated.Global", Justification = "Instantiated by Cocona")]
internal sealed class Commands
{
    private readonly IBulkPackageUpdater _bulkPackageUpdater;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;
    private readonly IGitRepositoryListLoader _gitRepositoryListLoader;

    public Commands(IGitRepositoryListLoader gitRepositoryListLoader, IBulkPackageUpdater bulkPackageUpdater)
    {
        this._gitRepositoryListLoader = gitRepositoryListLoader;
        this._bulkPackageUpdater = bulkPackageUpdater;
    }

    [Command("update-packages", Description = "Update all packages in all repositories")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Used by Cocona")]
    public async Task UpdatePackagesAsync([Option(name: "repositories", ['r'], Description = "repos.lst file containing list of repositories")] string repositoriesFileName,
                                          [Option(name: "template", ['m'], Description = "Template repository to clone")] string templateRepository,
                                          [Option(name: "cache", ['c'], Description = "package cache file")] string? cacheFileName,
                                          [Option(name: "tracking", ['t'], Description = "folder where to write tracking.json file")] string trackingFileName,
                                          [Option(name: "packages", ['p'], Description = "Packages.json file to load")] string packagesFileName,
                                          [Option(name: "work", ['w'], Description = "folder where to clone repositories")] string workFolder,
                                          [Option(name: "release", ['l'], Description = "release.config file to load")] string releaseConfigFileName,
                                          [Option(name: "source", ['s'], Description = "Urls to additional NuGet feeds to load")] IEnumerable<string>? source)
    {
        IReadOnlyList<string> repositories = ExcludeTemplateRepo(await this._gitRepositoryListLoader.LoadAsync(path: repositoriesFileName, cancellationToken: this._cancellationToken),
                                                                 templateRepository: templateRepository);

        if (repositories.Count == 0)
        {
            throw new InvalidOperationException("No Repositories found");
        }

        string[] nugetSources = source?.ToArray() ?? Array.Empty<string>();
        await this._bulkPackageUpdater.BulkUpdateAsync(templateRepository: templateRepository,
                                                       cacheFileName: cacheFileName,
                                                       trackingFileName: trackingFileName,
                                                       packagesFileName: packagesFileName,
                                                       workFolder: workFolder,
                                                       releaseConfigFileName: releaseConfigFileName,
                                                       additionalNugetSources: nugetSources,
                                                       repositories: repositories,
                                                       cancellationToken: this._cancellationToken);
    }

    [Command("update-template", Description = "Update repos from template in all repositories")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Used by Cocona")]
    public async Task UpdateFromTemplateAsync([Option(name: "repositories", ['r'], Description = "repos.lst file containing list of repositories")] string repositoriesFileName,
                                              [Option(name: "template", ['m'], Description = "Template repository to clone")] string templateRepository,
                                              [Option(name: "tracking", ['t'], Description = "folder where to write tracking.json file")] string trackingFileName,
                                              [Option(name: "packages", ['p'], Description = "Packages.json file to load")] string packagesFileName,
                                              [Option(name: "work", ['w'], Description = "folder where to clone repositories")] string workFolder,
                                              [Option(name: "release", ['l'], Description = "release.config file to load")] string releaseConfigFileName)
    {
        IReadOnlyList<string> repositories = ExcludeTemplateRepo(await this._gitRepositoryListLoader.LoadAsync(path: repositoriesFileName, cancellationToken: this._cancellationToken),
                                                                 templateRepository: templateRepository);

        if (repositories.Count == 0)
        {
            throw new InvalidOperationException("No Repositories found");
        }
    }

    [Command("code-cleanup", Description = "Perform code cleanup in all repositories")]
    [SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global", Justification = "Used by Cocona")]
    public async Task CodeCleanupAsync([Option(name: "repositories", ['r'], Description = "repos.lst file containing list of repositories")] string repositoriesFileName,
                                       [Option(name: "template", ['m'], Description = "Template repository to clone")] string templateRepository,
                                       [Option(name: "tracking", ['t'], Description = "folder where to write tracking.json file")] string trackingFileName,
                                       [Option(name: "packages", ['p'], Description = "Packages.json file to load")] string packagesFileName,
                                       [Option(name: "work", ['w'], Description = "folder where to clone repositories")] string workFolder,
                                       [Option(name: "release", ['l'], Description = "release.config file to load")] string releaseConfigFileName)
    {
        IReadOnlyList<string> repositories = ExcludeTemplateRepo(await this._gitRepositoryListLoader.LoadAsync(path: repositoriesFileName, cancellationToken: this._cancellationToken),
                                                                 templateRepository: templateRepository);

        if (repositories.Count == 0)
        {
            throw new InvalidOperationException("No Repositories found");
        }
    }

    private static IReadOnlyList<string> ExcludeTemplateRepo(IReadOnlyList<string> repositories, string templateRepository)
    {
        return repositories.Where(repositoryUrl => !StringComparer.InvariantCultureIgnoreCase.Equals(x: templateRepository, y: repositoryUrl))
                           .ToArray();
    }
}