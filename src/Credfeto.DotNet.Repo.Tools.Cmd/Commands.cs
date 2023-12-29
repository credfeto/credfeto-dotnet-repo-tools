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
internal sealed class Commands
{
    private readonly IBulkPackageUpdater _bulkPackageUpdater;
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
        CancellationToken cancellationToken = CancellationToken.None;
        IReadOnlyList<string> repositories = ExcludeTemplateRepo(await this._gitRepositoryListLoader.LoadAsync(path: repositoriesFileName, cancellationToken: cancellationToken),
                                                                 templateRepository: templateRepository);

        if (repositories.Count == 0)
        {
            throw new InvalidOperationException("No Repositories found");
        }

        await this._bulkPackageUpdater.BulkUpdateAsync(additionalNugetSources: source?.ToArray() ?? Array.Empty<string>(),
                                                       templateRepository: templateRepository,
                                                       cacheFileName: cacheFileName,
                                                       trackingFileName: trackingFileName,
                                                       packagesFileName: packagesFileName,
                                                       workFolder: workFolder,
                                                       releaseConfigFileName: releaseConfigFileName,
                                                       repositories: repositories,
                                                       cancellationToken: cancellationToken);
    }

    private static IReadOnlyList<string> ExcludeTemplateRepo(IReadOnlyList<string> repositories, string templateRepository)
    {
        return repositories.Where(repositoryUrl => !StringComparer.InvariantCultureIgnoreCase.Equals(x: templateRepository, y: repositoryUrl))
                           .ToArray();
    }
}