using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Credfeto.DotNet.Repo.Tools.Git;

public static class GitUtils
{
    private static readonly CloneOptions GitCloneOptions = new() { Checkout = true, IsBare = false, RecurseSubmodules = true, FetchOptions = { Prune = true, TagFetchMode = TagFetchMode.All } };

    public static string GetWorkingDirectoryForRepository(string repoUrl)
    {
        string work = repoUrl.TrimEnd('/');

        // Extract the folder from the repo name
        string folder = work.Substring(work.LastIndexOf('/') + 1);

        int lastDot = folder.LastIndexOf('.');

        if (lastDot > 0)
        {
            return folder.Substring(startIndex: 0, length: lastDot);
        }

        return folder;
    }

    public static ValueTask<IGitRepository> OpenOrCloneAsync(string workDir, string repoUrl, in CancellationToken cancellationToken)
    {
        string workingDirectory = Path.Combine(path1: workDir, GetWorkingDirectoryForRepository(repoUrl));

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

    internal static bool IsHttps(string repoUrl)
    {
        return repoUrl.StartsWith(value: "https://", comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}