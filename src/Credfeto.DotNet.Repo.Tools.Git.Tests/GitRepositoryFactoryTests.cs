using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class GitRepositoryFactoryTests : LoggingFolderCleanupTestBase
{
    private readonly IGitRepositoryFactory _gitRepositoryFactory;

    public GitRepositoryFactoryTests(ITestOutputHelper output)
        : base(output)
    {
        IGitRepositoryLocator gitRepositoryLocator = GetSubstitute<IGitRepositoryLocator>();
        gitRepositoryLocator
            .GetWorkingDirectory(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Path.Combine(path1: this.TempFolder, path2: "scratch"));

        this._gitRepositoryFactory = new GitRepositoryFactory(
            locator: gitRepositoryLocator,
            this.GetTypedLogger<GitRepositoryFactory>()
        );
    }

    [Fact]
    public Task OpenOrCloneAsync_WithSshUrlAndNonExistentDirectory_ThrowsGitException()
    {
        return Assert.ThrowsAsync<GitException>(() =>
            this
                ._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: this.TempFolder,
                    repoUrl: "/nonexistent/path/repo.git",
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task OpenOrCloneAsync_WithExistingDirectoryAndLockJson_DeletesAndThrowsGitException()
    {
        string scratchDir = Path.Combine(path1: this.TempFolder, path2: "scratch");
        Directory.CreateDirectory(scratchDir);

        string gitDir = Path.Combine(scratchDir, ".git");
        Directory.CreateDirectory(gitDir);

        string lockFile = Path.Combine(path1: scratchDir, path2: ".git", path3: "lock.json");
        await File.WriteAllTextAsync(path: lockFile, contents: "{}", cancellationToken: this.CancellationToken());

        await Assert.ThrowsAsync<GitException>(() =>
            this
                ._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: this.TempFolder,
                    repoUrl: "/nonexistent/path/repo.git",
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );

        Assert.False(
            condition: Directory.Exists(scratchDir),
            userMessage: "Directory with lock.json should have been deleted"
        );
    }

    [Fact]
    public async Task OpenOrCloneAsync_WithExistingGitRepoAndNoLockJson_ThrowsWhenResetFails()
    {
        string scratchDir = Path.Combine(path1: this.TempFolder, path2: "scratch");
        await CreateMinimalGitRepoAsync(repoPath: scratchDir, cancellationToken: this.CancellationToken());

        await Assert.ThrowsAsync<GitException>(() =>
            this
                ._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: this.TempFolder,
                    repoUrl: "https://example.com/nonexistent.git",
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact(Skip = "Requires SSH to be setup")]
    public Task CanCloneSshAsync()
    {
        return this.CloneTestCommonAsync(uri: Repositories.GitHubSsh, cancellationToken: this.CancellationToken());
    }

    [Fact(Skip = "Requires SSH to be setup")]
    public async Task CreateBranchSshAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        using (
            IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: Repositories.GitHubSsh,
                cancellationToken: cancellationToken
            )
        )
        {
            this.Output.WriteLine($"Repo: {repo.ClonePath}");

            string newBranch = GenerateDeleteMeBranchName();

            if (repo.DoesBranchExist(branchName: newBranch))
            {
                repo.RemoveAllLocalBranches();
            }

            await repo.CreateBranchAsync(branchName: newBranch, cancellationToken: cancellationToken);

            Assert.True(repo.DoesBranchExist(branchName: newBranch), userMessage: "Branch should exist");

            await repo.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

            await repo.RemoveBranchesForPrefixAsync(
                branchForUpdate: "depends/delete-me",
                branchPrefix: "depends/",
                upstream: GitConstants.Upstream,
                cancellationToken: cancellationToken
            );

            repo.RemoveAllLocalBranches();

            Assert.False(repo.DoesBranchExist(branchName: newBranch), userMessage: "Branch should not exist");
        }
    }

    private static string GenerateDeleteMeBranchName()
    {
        return $"delete-me/{GenerateUniqueBranchId()}";
    }

    private static string GenerateUniqueBranchId()
    {
        return Guid.NewGuid()
            .ToString()
            .Replace(oldValue: "-", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static async Task CreateMinimalGitRepoAsync(string repoPath, CancellationToken cancellationToken)
    {
        string emptyHooksDir = Path.Combine(Path.GetTempPath(), "empty-hooks-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyHooksDir);
        Directory.CreateDirectory(repoPath);

        await RunGitAsync(
            repoPath: repoPath,
            arguments: "init --initial-branch=feature/test",
            cancellationToken: cancellationToken
        );
        await RunGitAsync(
            repoPath: repoPath,
            arguments: $"config core.hooksPath \"{emptyHooksDir}\"",
            cancellationToken: cancellationToken
        );
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "config user.email \"test@example.com\"",
            cancellationToken: cancellationToken
        );
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "config user.name \"Test User\"",
            cancellationToken: cancellationToken
        );
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "config commit.gpgsign false",
            cancellationToken: cancellationToken
        );

        await File.WriteAllTextAsync(
            path: Path.Combine(repoPath, "README.md"),
            contents: "# Test\n",
            cancellationToken: cancellationToken
        );

        await RunGitAsync(repoPath: repoPath, arguments: "add -A", cancellationToken: cancellationToken);
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "commit -m \"Initial commit\"",
            cancellationToken: cancellationToken
        );
    }

    private static async Task RunGitAsync(string repoPath, string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repoPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git");

        await process.WaitForExitAsync(cancellationToken);
    }

    private async Task CloneTestCommonAsync(string uri, CancellationToken cancellationToken)
    {
        using (
            IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: uri,
                cancellationToken: cancellationToken
            )
        )
        {
            this.Output.WriteLine($"Repo: {repo.ClonePath}");

            string branch = repo.GetDefaultBranch(upstream: GitConstants.Upstream);
            this.Output.WriteLine($"Default Branch: {branch}");

            IReadOnlyCollection<string> remoteBranches = repo.GetRemoteBranches(upstream: GitConstants.Upstream);

            foreach (string remoteBranch in remoteBranches)
            {
                this.Output.WriteLine($"* Branch: {remoteBranch}");
            }
        }

        using (
            IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: uri,
                cancellationToken: cancellationToken
            )
        )
        {
            repo.RemoveAllLocalBranches();
        }
    }
}
