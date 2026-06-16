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
    public async Task ExecAsync_WithoutGitFolder_RunsSuccessfully()
    {
        string workDir = Path.Combine(this.TempFolder, "no-git");
        Directory.CreateDirectory(workDir);

        (string[] output, int exitCode) = await GitCommandLine.ExecAsync(
            clonePath: "fake-clone",
            repoPath: workDir,
            arguments: "--version",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: exitCode);
        Assert.NotEmpty(output);
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
            arguments: "--version",
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
            arguments: "--version",
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
                    arguments: "--version",
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }
}
