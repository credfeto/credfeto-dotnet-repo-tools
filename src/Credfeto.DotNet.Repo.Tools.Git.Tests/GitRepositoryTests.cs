using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using FunFair.Test.Common;
using LibGit2Sharp;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class GitRepositoryTests : LoggingFolderCleanupTestBase
{
    private const string DEFAULT_BRANCH = "feature/test";

    public GitRepositoryTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task Constructor_SetsClonePathAndWorkingDirectory()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.Equal(expected: "https://example.com/repo.git", actual: repo.ClonePath);
        Assert.Equal(expected: repoPath, actual: repo.WorkingDirectory);
    }

    [Fact]
    public async Task Dispose_WithNullRepo_DoesNotThrow()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        repo.Dispose();
    }

    [Fact]
    public async Task Dispose_AfterAccessingActiveRepo_DoesNotThrow()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        _ = repo.HeadRev;

        repo.Dispose();
    }

    [Fact]
    public async Task Active_WithNullRepo_OpensRepositoryFromWorkingDirectory()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Repository active = repo.Active;

        Assert.NotNull(active);
    }

    [Fact]
    public async Task HeadRev_ReturnsNonEmptyString()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        string headRev = repo.HeadRev;

        Assert.NotEmpty(headRev);
        Assert.Equal(expected: 40, actual: headRev.Length);
    }

    [Fact]
    public async Task HasSubmodules_WithNoSubmodules_ReturnsFalse()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.False(condition: repo.HasSubmodules, userMessage: "Should have no submodules");
    }

    [Fact]
    public async Task HasUncommittedChanges_WithCleanRepo_ReturnsFalse()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.False(condition: repo.HasUncommittedChanges(), userMessage: "Should have no uncommitted changes");
    }

    [Fact]
    public async Task HasUncommittedChanges_WithUncommittedFile_ReturnsTrue()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        await File.WriteAllTextAsync(
            path: Path.Combine(repoPath, "new-file.txt"),
            contents: "new content\n",
            cancellationToken: this.CancellationToken()
        );

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.True(condition: repo.HasUncommittedChanges(), userMessage: "Should have uncommitted changes");
    }

    [Fact]
    public async Task DoesBranchExist_WithDefaultBranch_ReturnsTrue()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.True(
            condition: repo.DoesBranchExist(DEFAULT_BRANCH),
            userMessage: $"Default branch '{DEFAULT_BRANCH}' should exist"
        );
    }

    [Fact]
    public async Task DoesBranchExist_WithNonExistentBranch_ReturnsFalse()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.False(
            condition: repo.DoesBranchExist("branch-that-does-not-exist"),
            userMessage: "Non-existent branch should not exist"
        );
    }

    [Fact]
    public async Task GetLastCommitDate_ReturnsValidDate()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        DateTimeOffset lastCommitDate = repo.GetLastCommitDate();

        Assert.NotEqual(expected: DateTimeOffset.MinValue, actual: lastCommitDate);
        Assert.NotEqual(expected: DateTimeOffset.MaxValue, actual: lastCommitDate);
    }

    [Fact]
    public async Task GetRemoteBranches_WithNoRemote_ReturnsEmptyCollection()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        IReadOnlyCollection<string> branches = repo.GetRemoteBranches("origin");

        Assert.Empty(branches);
    }

    [Fact]
    public async Task GetDefaultBranch_WithNoRemote_ThrowsGitException()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        Assert.Throws<GitException>(() => repo.GetDefaultBranch("origin"));
    }

    [Fact]
    public async Task ResetToDefaultBranchAsync_WithNoRemote_ThrowsGitException()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await Assert.ThrowsAsync<GitException>(() =>
            repo.ResetToDefaultBranchAsync(upstream: "origin", cancellationToken: this.CancellationToken()).AsTask()
        );
    }

    [Fact]
    public async Task GetDefaultBranch_WithLocalRemoteAndHeadBranch_ReturnsDefaultBranch()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());
        string bareRemotePath = await this.CreateLocalBareRemoteAsync(
            sourceRepoPath: repoPath,
            cancellationToken: this.CancellationToken()
        );

        await RunGitAsync(
            repoPath: repoPath,
            arguments: $"remote add origin \"{bareRemotePath}\"",
            cancellationToken: this.CancellationToken()
        );
        await RunGitAsync(repoPath: repoPath, arguments: "fetch origin", cancellationToken: this.CancellationToken());
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "remote set-head origin -a",
            cancellationToken: this.CancellationToken()
        );

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        string defaultBranch = repo.GetDefaultBranch("origin");

        Assert.Equal(expected: DEFAULT_BRANCH, actual: defaultBranch);
    }

    [Fact]
    public async Task ResetToDefaultBranchAsync_WithLocalRemote_Succeeds()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());
        string bareRemotePath = await this.CreateLocalBareRemoteAsync(
            sourceRepoPath: repoPath,
            cancellationToken: this.CancellationToken()
        );

        await RunGitAsync(
            repoPath: repoPath,
            arguments: $"remote add origin \"{bareRemotePath}\"",
            cancellationToken: this.CancellationToken()
        );
        await RunGitAsync(repoPath: repoPath, arguments: "fetch origin", cancellationToken: this.CancellationToken());
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "remote set-head origin -a",
            cancellationToken: this.CancellationToken()
        );
        await RunGitAsync(
            repoPath: repoPath,
            arguments: $"branch --set-upstream-to=origin/{DEFAULT_BRANCH} {DEFAULT_BRANCH}",
            cancellationToken: this.CancellationToken()
        );

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.ResetToDefaultBranchAsync(upstream: "origin", cancellationToken: this.CancellationToken());

        Assert.False(
            condition: repo.HasUncommittedChanges(),
            userMessage: "Should have no uncommitted changes after reset"
        );
    }

    [Fact]
    public async Task RemoveAllLocalBranches_WithNoBranchesToRemove_DoesNotThrow()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        repo.RemoveAllLocalBranches();
    }

    [Fact]
    public async Task CreateBranchAsync_CreatesNewBranch()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        const string newBranch = "feature/unit-test-branch";

        await repo.CreateBranchAsync(branchName: newBranch, cancellationToken: this.CancellationToken());

        Assert.True(
            condition: repo.DoesBranchExist(newBranch),
            userMessage: $"Branch '{newBranch}' should exist after creation"
        );
    }

    [Fact]
    public async Task RemoveAllLocalBranches_WithOtherBranches_RemovesNonCurrentBranches()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        const string extraBranch = "feature/extra-branch";
        await repo.CreateBranchAsync(branchName: extraBranch, cancellationToken: this.CancellationToken());

        await repo.SwitchBranchAsync(branchName: DEFAULT_BRANCH, cancellationToken: this.CancellationToken());

        repo.RemoveAllLocalBranches();

        Assert.False(
            condition: repo.DoesBranchExist(extraBranch),
            userMessage: $"Branch '{extraBranch}' should have been removed"
        );
    }

    [Fact]
    public async Task CommitAsync_WithUnstagedChanges_CommitsSuccessfully()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        await File.WriteAllTextAsync(
            path: Path.Combine(repoPath, "commit-test.txt"),
            contents: "test content\n",
            cancellationToken: this.CancellationToken()
        );

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.CommitAsync(message: "Test commit", cancellationToken: this.CancellationToken());

        Assert.False(
            condition: repo.HasUncommittedChanges(),
            userMessage: "Should have no uncommitted changes after commit"
        );
    }

    [Fact]
    public async Task CommitNamedAsync_WithSpecificFile_CommitsSuccessfully()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        string namedFile = Path.Combine(repoPath, "named-commit-test.txt");
        await File.WriteAllTextAsync(
            path: namedFile,
            contents: "test content\n",
            cancellationToken: this.CancellationToken()
        );

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.CommitNamedAsync(
            message: "Named test commit",
            files: ["named-commit-test.txt"],
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: repo.HasUncommittedChanges(),
            userMessage: "Should have no uncommitted changes after named commit"
        );
    }

    [Fact]
    public async Task PushAsync_WithNoRemote_LogsButDoesNotThrow()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.PushAsync(cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task PushOriginAsync_WithNoRemote_LogsButDoesNotThrow()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.PushOriginAsync(
            branchName: DEFAULT_BRANCH,
            upstream: "origin",
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task RemoveBranchesForPrefixAsync_WithNoMatchingBranches_DoesNotThrow()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.RemoveBranchesForPrefixAsync(
            branchForUpdate: "depends/some-dep",
            branchPrefix: "depends/",
            upstream: "origin",
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task RemoveBranchesForPrefixAsync_WithBranchMatchingExactBranchForUpdate_SkipsBranch()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        const string branchForUpdate = "depends/exact-match";
        await repo.CreateBranchAsync(branchName: branchForUpdate, cancellationToken: this.CancellationToken());

        await repo.SwitchBranchAsync(branchName: DEFAULT_BRANCH, cancellationToken: this.CancellationToken());

        await repo.RemoveBranchesForPrefixAsync(
            branchForUpdate: branchForUpdate,
            branchPrefix: "depends/",
            upstream: "origin",
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: repo.DoesBranchExist(branchForUpdate),
            userMessage: $"Branch '{branchForUpdate}' should have been skipped (it is branchForUpdate)"
        );
    }

    [Fact]
    public async Task RemoveBranchesForPrefixAsync_WithBranchMatchingPrefix_DeletesBranch()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());
        await AddFakeRemoteAsync(repoPath: repoPath, cancellationToken: this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        const string deletableBranch = "depends/old-dep";
        await repo.CreateBranchAsync(branchName: deletableBranch, cancellationToken: this.CancellationToken());

        await repo.SwitchBranchAsync(branchName: DEFAULT_BRANCH, cancellationToken: this.CancellationToken());

        await repo.RemoveBranchesForPrefixAsync(
            branchForUpdate: "depends/new-dep",
            branchPrefix: "depends/",
            upstream: "origin",
            cancellationToken: this.CancellationToken()
        );

        Assert.False(
            condition: repo.DoesBranchExist(deletableBranch),
            userMessage: $"Branch '{deletableBranch}' should have been deleted"
        );
    }

    [Fact]
    public async Task RemoveBranchesForPrefixAsync_WithCurrentBranchMatchingPrefix_SkipsCurrentBranch()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.CreateBranchAsync(branchName: "depends/current-branch", cancellationToken: this.CancellationToken());

        await repo.RemoveBranchesForPrefixAsync(
            branchForUpdate: "depends/other",
            branchPrefix: "depends/",
            upstream: "origin",
            cancellationToken: this.CancellationToken()
        );

        Assert.True(
            condition: repo.DoesBranchExist("depends/current-branch"),
            userMessage: "Current branch should not have been deleted"
        );
    }

    [Fact]
    public async Task SwitchBranchAsync_ToBranchThatExists_SwitchesSuccessfully()
    {
        string repoPath = await this.CreateTempGitRepoAsync(this.CancellationToken());

        using GitRepository repo = new(
            clonePath: "https://example.com/repo.git",
            workingDirectory: repoPath,
            repo: null,
            logger: this.GetTypedLogger<GitRepositoryFactory>()
        );

        await repo.CreateBranchAsync(branchName: "feature/switch-test", cancellationToken: this.CancellationToken());

        await repo.SwitchBranchAsync(branchName: DEFAULT_BRANCH, cancellationToken: this.CancellationToken());

        using Repository libGitRepo = new(repoPath);
        Assert.Equal(expected: DEFAULT_BRANCH, actual: libGitRepo.Head.FriendlyName);
    }

    private async Task<string> CreateLocalBareRemoteAsync(string sourceRepoPath, CancellationToken cancellationToken)
    {
        string bareRemotePath = Path.Combine(
            this.TempFolder,
            "bare-remote-" + Guid.NewGuid().ToString("N")[..8] + ".git"
        );

        await RunGitAsync(
            repoPath: this.TempFolder,
            arguments: $"clone --bare \"{sourceRepoPath}\" \"{bareRemotePath}\"",
            cancellationToken: cancellationToken
        );

        await RunGitAsync(
            repoPath: bareRemotePath,
            arguments: $"symbolic-ref HEAD refs/heads/{DEFAULT_BRANCH}",
            cancellationToken: cancellationToken
        );

        return bareRemotePath;
    }

    private static Task AddFakeRemoteAsync(string repoPath, in CancellationToken cancellationToken)
    {
        return RunGitAsync(
            repoPath: repoPath,
            arguments: "remote add origin /nonexistent/fake/remote.git",
            cancellationToken: cancellationToken
        );
    }

    private async Task<string> CreateTempGitRepoAsync(CancellationToken cancellationToken)
    {
        string repoPath = Path.Combine(this.TempFolder, "git-repo-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(repoPath);

        string emptyHooksPath = Path.Combine(this.TempFolder, "empty-hooks");
        Directory.CreateDirectory(emptyHooksPath);

        await RunGitAsync(
            repoPath: repoPath,
            arguments: $"init --initial-branch={DEFAULT_BRANCH}",
            cancellationToken: cancellationToken
        );
        await RunGitAsync(
            repoPath: repoPath,
            arguments: $"config core.hooksPath \"{emptyHooksPath}\"",
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
            contents: "# Test Repo\n",
            cancellationToken: cancellationToken
        );

        await RunGitAsync(repoPath: repoPath, arguments: "add -A", cancellationToken: cancellationToken);
        await RunGitAsync(
            repoPath: repoPath,
            arguments: "commit -m \"Initial commit\"",
            cancellationToken: cancellationToken
        );

        return repoPath;
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
}
