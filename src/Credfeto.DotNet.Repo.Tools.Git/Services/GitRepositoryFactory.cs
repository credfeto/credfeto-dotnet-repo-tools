using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Helpers;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using LibGit2Sharp;

namespace Credfeto.DotNet.Repo.Tools.Git.Services;

public sealed class GitRepositoryFactory : IGitRepositoryFactory
{
    private static readonly CloneOptions GitCloneOptions = new() { Checkout = true, IsBare = false, RecurseSubmodules = true, FetchOptions = { Prune = true, TagFetchMode = TagFetchMode.All } };

    private readonly IGitRepositoryLocator _locator;

    public GitRepositoryFactory(IGitRepositoryLocator locator)
    {
        this._locator = locator;
    }

    public ValueTask<IGitRepository> OpenOrCloneAsync(string workDir, string repoUrl, in CancellationToken cancellationToken)
    {
        string workingDirectory = this._locator.GetWorkingDirectory(workDir: workDir, repoUrl: repoUrl);

        return Directory.Exists(workingDirectory)
            ? OpenRepoAsync(repoUrl: repoUrl, workingDirectory: workingDirectory, cancellationToken: cancellationToken)
            : CloneRepositoryAsync(workDir: workDir, destinationPath: workingDirectory, repoUrl: repoUrl, cancellationToken: cancellationToken);
    }

    private static async ValueTask<IGitRepository> OpenRepoAsync(string repoUrl, string workingDirectory, CancellationToken cancellationToken)
    {
        IGitRepository? repo = null;

        try
        {
            repo = new GitRepository(clonePath: repoUrl, workingDirectory: workingDirectory, new(Repository.Discover(workingDirectory)));

            await repo.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

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

    private static async ValueTask<IGitRepository> CloneRepositoryAsync(string workDir, string destinationPath, string repoUrl, CancellationToken cancellationToken)
    {
        string? path = IsHttps(repoUrl)
            ? Repository.Clone(sourceUrl: repoUrl, workdirPath: destinationPath, options: GitCloneOptions)
            : await CloneSshAsync(sourceUrl: repoUrl, workdirPath: workDir, destinationPath: destinationPath, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new GitException($"Failed to clone repo {repoUrl} to {workDir}");
        }

        return new GitRepository(clonePath: repoUrl, workingDirectory: destinationPath, new(Repository.Discover(path)));
    }

    private static async ValueTask<string?> CloneSshAsync(string sourceUrl, string workdirPath, string destinationPath, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: workdirPath, $"clone --recurse-submodules {sourceUrl} {destinationPath}", cancellationToken: cancellationToken);

        return destinationPath;
    }

    private static bool IsHttps(string repoUrl)
    {
        return repoUrl.StartsWith(value: "https://", comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}