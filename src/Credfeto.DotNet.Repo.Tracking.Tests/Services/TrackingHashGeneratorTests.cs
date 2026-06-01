using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tracking.Tests.Services;

public sealed class TrackingHashGeneratorTests : LoggingFolderCleanupTestBase
{
    private readonly ITrackingHashGenerator _hashGenerator;

    public TrackingHashGeneratorTests(ITestOutputHelper output)
        : base(output)
    {
        this._hashGenerator = new TrackingHashGenerator();

        if (!Directory.Exists(this.TempFolder))
        {
            Directory.CreateDirectory(this.TempFolder);
        }
    }

    [Fact]
    public async Task GenerateTrackingHashAsyncWithEmptyDirectoryReturnsNonEmptyStringAsync()
    {
        // An empty working directory contains no files matching the masks.
        // The hash must still be returned as a non-null, non-empty base64 string.
        string emptyDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        RepoContext repoContext = new(
            ClonePath: emptyDir,
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: emptyDir,
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );

        string hash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public async Task GenerateTrackingHashAsyncWithMatchingFilesReturnsDifferentHashFromEmptyAsync()
    {
        // A working directory containing files matching the tracked masks must produce
        // a different hash from an empty directory.
        string emptyDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        string populatedDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(populatedDir);

        await File.WriteAllTextAsync(
            path: Path.Combine(populatedDir, "test.sln"),
            contents: "# solution file",
            cancellationToken: this.CancellationToken()
        );

        await File.WriteAllTextAsync(
            path: Path.Combine(populatedDir, "test.csproj"),
            contents: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        await File.WriteAllTextAsync(
            path: Path.Combine(populatedDir, "test.props"),
            contents: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        RepoContext emptyContext = new(
            ClonePath: emptyDir,
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: emptyDir,
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );

        RepoContext populatedContext = new(
            ClonePath: populatedDir,
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: populatedDir,
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );

        string emptyHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: emptyContext,
            cancellationToken: this.CancellationToken()
        );

        string populatedHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: populatedContext,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEqual(expected: emptyHash, actual: populatedHash);
    }

    [Fact]
    public async Task GenerateTrackingHashAsyncIsDeterministicForSameFilesAsync()
    {
        // Calling the hash generator twice on the same directory with the same files
        // must return the same hash both times.
        string workDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        await File.WriteAllTextAsync(
            path: Path.Combine(workDir, "test.sln"),
            contents: "# solution file",
            cancellationToken: this.CancellationToken()
        );

        await File.WriteAllTextAsync(
            path: Path.Combine(workDir, "test.csproj"),
            contents: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        RepoContext repoContext = new(
            ClonePath: workDir,
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: workDir,
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );

        string firstHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        string secondHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: firstHash, actual: secondHash);
    }
}
