using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Helpers;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Git.Services.LoggingExtensions;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Git.Services;

public sealed class GitRepositoryFactory : IGitRepositoryFactory
{
    private static readonly CloneOptions GitCloneOptions = new()
    {
        Checkout = true,
        IsBare = false,
        RecurseSubmodules = true,
        FetchOptions = { Prune = true, TagFetchMode = TagFetchMode.All },
    };

    private readonly IGitRepositoryLocator _locator;
    private readonly ILogger<GitRepositoryFactory> _logger;

    public GitRepositoryFactory(IGitRepositoryLocator locator, ILogger<GitRepositoryFactory> logger)
    {
        this._locator = locator;
        this._logger = logger;
    }

    public ValueTask<IGitRepository> OpenOrCloneAsync(
        string workDir,
        string repoUrl,
        in CancellationToken cancellationToken
    )
    {
        string workingDirectory = this._locator.GetWorkingDirectory(
            workDir: workDir,
            repoUrl: repoUrl
        );

        if (Directory.Exists(workingDirectory))
        {
            string lockFile = Path.Combine(
                path1: workingDirectory,
                path2: ".git",
                path3: "lock.json"
            );

            if (!File.Exists(lockFile))
            {
                return this.OpenRepoAsync(
                    repoUrl: repoUrl,
                    workingDirectory: workingDirectory,
                    cancellationToken: cancellationToken
                );
            }

            this._logger.DestroyingAndReCloning(repoUrl: repoUrl, repoPath: workingDirectory);

            // clear repo before cloning - no idea what the state is going to be at this point
            Directory.Delete(path: workingDirectory, recursive: true);
        }

        return this.CloneRepositoryAsync(
            workDir: workDir,
            destinationPath: workingDirectory,
            repoUrl: repoUrl,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<IGitRepository> OpenRepoAsync(
        string repoUrl,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        this._logger.OpeningRepo(repoUrl: repoUrl, repoPath: workingDirectory);
        IGitRepository? repo = null;

        try
        {
            repo = new GitRepository(
                clonePath: repoUrl,
                workingDirectory: workingDirectory,
                new(Repository.Discover(workingDirectory)),
                logger: this._logger
            );

            await repo.ResetToMasterAsync(
                upstream: GitConstants.Upstream,
                cancellationToken: cancellationToken
            );

            // Start with a clean slate - branches will be created as needed
            repo.RemoveAllLocalBranches();

            return repo;
        }
        catch
        {
            repo?.Dispose();

            throw;
        }
    }

    private async ValueTask<IGitRepository> CloneRepositoryAsync(
        string workDir,
        string destinationPath,
        string repoUrl,
        CancellationToken cancellationToken
    )
    {
        this._logger.CloningRepo(repoUrl: repoUrl, repoPath: destinationPath);

        string? path = IsHttps(repoUrl)
            ? Repository.Clone(
                sourceUrl: repoUrl,
                workdirPath: destinationPath,
                options: GitCloneOptions
            )
            : await CloneSshAsync(
                sourceUrl: repoUrl,
                workdirPath: workDir,
                destinationPath: destinationPath,
                cancellationToken: cancellationToken
            );

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new GitException($"Failed to clone repo {repoUrl} to {workDir}");
        }

        return new GitRepository(
            clonePath: repoUrl,
            workingDirectory: destinationPath,
            new(Repository.Discover(path)),
            logger: this._logger
        );
    }

    private static async ValueTask<string?> CloneSshAsync(
        string sourceUrl,
        string workdirPath,
        string destinationPath,
        CancellationToken cancellationToken
    )
    {
        await GitCommandLine.ExecAsync(
            clonePath: sourceUrl,
            repoPath: workdirPath,
            $"clone --recurse-submodules {sourceUrl} {destinationPath}",
            cancellationToken: cancellationToken
        );

        return destinationPath;
    }

    private static bool IsHttps(string repoUrl)
    {
        return repoUrl.StartsWith(
            value: "https://",
            comparisonType: StringComparison.OrdinalIgnoreCase
        );
    }
}
