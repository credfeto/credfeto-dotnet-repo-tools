using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Helpers;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class GitCommandLineTests : LoggingFolderCleanupTestBase
{
    public GitCommandLineTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public void RemoveRepositoryOverrides_RemovesGitRepositoryVariablesOnly()
    {
        ProcessStartInfo psi = new() { FileName = "git" };
        psi.Environment["GIT_DIR"] = "/elsewhere/.git";
        psi.Environment["GIT_WORK_TREE"] = "/elsewhere";
        psi.Environment["GIT_INDEX_FILE"] = "/elsewhere/.git/index";
        psi.Environment["GIT_PREFIX"] = "sub/";
        psi.Environment["GIT_OBJECT_DIRECTORY"] = "/elsewhere/.git/objects";
        psi.Environment["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = "/elsewhere/alt";
        psi.Environment["GIT_AUTHOR_NAME"] = "Test User";

        GitCommandLine.RemoveRepositoryOverrides(psi);

        Assert.False(psi.Environment.ContainsKey("GIT_DIR"), userMessage: "GIT_DIR should be removed");
        Assert.False(psi.Environment.ContainsKey("GIT_WORK_TREE"), userMessage: "GIT_WORK_TREE should be removed");
        Assert.False(psi.Environment.ContainsKey("GIT_INDEX_FILE"), userMessage: "GIT_INDEX_FILE should be removed");
        Assert.False(psi.Environment.ContainsKey("GIT_PREFIX"), userMessage: "GIT_PREFIX should be removed");
        Assert.False(
            psi.Environment.ContainsKey("GIT_OBJECT_DIRECTORY"),
            userMessage: "GIT_OBJECT_DIRECTORY should be removed"
        );
        Assert.False(
            psi.Environment.ContainsKey("GIT_ALTERNATE_OBJECT_DIRECTORIES"),
            userMessage: "GIT_ALTERNATE_OBJECT_DIRECTORIES should be removed"
        );
        Assert.Equal(expected: "Test User", actual: psi.Environment["GIT_AUTHOR_NAME"]);
    }

    [Fact]
    public async Task ExecAsync_WithoutGitFolder_RunsSuccessfully()
    {
        string workDir = Path.Combine(this.TempFolder, "no-git");
        Directory.CreateDirectory(workDir);

        (string[] output, int exitCode) = await GitCommandLine.ExecAsync(
            clonePath: "fake-clone",
            repoPath: workDir,
            arguments: ["--version"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: exitCode);
        Assert.NotEmpty(output);
    }

    [Fact]
    public async Task ExecAsync_WhenGitWritesToStandardError_SurfacesStandardErrorInOutputAsync()
    {
        string workDir = Path.Combine(this.TempFolder, "no-repo-for-status");
        Directory.CreateDirectory(workDir);

        (string[] output, int exitCode) = await GitCommandLine.ExecAsync(
            clonePath: "fake-clone",
            repoPath: workDir,
            arguments: ["status"],
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEqual(expected: 0, actual: exitCode);
        Assert.Contains(
            output,
            line => line.Contains(value: "not a git repository", comparisonType: StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task ExecAsync_WithGitFolderButNoLockFiles_RunsSuccessfully()
    {
        string workDir = Path.Combine(this.TempFolder, "with-git-no-locks");
        string gitDir = Path.Combine(workDir, ".git");
        Directory.CreateDirectory(gitDir);

        (_, int exitCode) = await GitCommandLine.ExecAsync(
            clonePath: "fake-clone",
            repoPath: workDir,
            arguments: ["--version"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task ExecAsync_WithLockFileInObjectsFolder_RunsSuccessfully()
    {
        string workDir = Path.Combine(this.TempFolder, "with-objects-lock");
        string gitDir = Path.Combine(workDir, ".git");
        string objectsDir = Path.Combine(gitDir, "objects");
        Directory.CreateDirectory(objectsDir);

        await File.WriteAllTextAsync(
            path: Path.Combine(objectsDir, "pack.lock"),
            contents: "lock",
            cancellationToken: this.CancellationToken()
        );

        (_, int exitCode) = await GitCommandLine.ExecAsync(
            clonePath: "fake-clone",
            repoPath: workDir,
            arguments: ["--version"],
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: exitCode);
    }

    [Fact]
    public async Task ExecAsync_WithLockFileOutsideObjectsFolder_ThrowsGitRepositoryLockedException()
    {
        string workDir = Path.Combine(this.TempFolder, "with-index-lock");
        string gitDir = Path.Combine(workDir, ".git");
        Directory.CreateDirectory(gitDir);

        await File.WriteAllTextAsync(
            path: Path.Combine(gitDir, "index.lock"),
            contents: "lock",
            cancellationToken: this.CancellationToken()
        );

        await Assert.ThrowsAsync<GitRepositoryLockedException>(() =>
            GitCommandLine
                .ExecAsync(
                    clonePath: "fake-clone",
                    repoPath: workDir,
                    arguments: ["--version"],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }
}
