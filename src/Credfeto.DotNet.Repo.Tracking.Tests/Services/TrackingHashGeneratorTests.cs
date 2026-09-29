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

        Assert.NotEqual(expected: emptyHash, actual: populatedHash, comparer: StringComparer.Ordinal);
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

    [Fact]
    public async Task GenerateTrackingHashAsyncIgnoresFilesInObjBinAndGitDirectoriesAsync()
    {
        // Files under obj/, bin/ and .git/ are generated or metadata content that varies
        // between a freshly built tree and a freshly cleaned tree for identical source,
        // so they must not influence the tracking hash.
        string workDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

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

        string hashBeforeGeneratedFiles = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        string objDir = Path.Combine(workDir, "obj");
        Directory.CreateDirectory(objDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(objDir, "test.csproj.nuget.g.props"),
            contents: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        string binDir = Path.Combine(workDir, "bin");
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(binDir, "whatever.props"),
            contents: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        string gitDir = Path.Combine(workDir, ".git");
        Directory.CreateDirectory(gitDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(gitDir, "some.props"),
            contents: "<Project />",
            cancellationToken: this.CancellationToken()
        );

        string hashAfterGeneratedFiles = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: hashBeforeGeneratedFiles, actual: hashAfterGeneratedFiles);
    }

    [Fact]
    public async Task GenerateTrackingHashAsyncChangesWhenSourceLevelPropsFileChangesAsync()
    {
        // Source-level files outside obj/, bin/ and .git/ must still be hashed, so a change
        // to one must change the resulting hash.
        string workDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        string propsPath = Path.Combine(workDir, "test.props");
        await File.WriteAllTextAsync(
            path: propsPath,
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

        await File.WriteAllTextAsync(
            path: propsPath,
            contents: "<Project><PropertyGroup /></Project>",
            cancellationToken: this.CancellationToken()
        );

        string secondHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEqual(expected: firstHash, actual: secondHash, comparer: StringComparer.Ordinal);
    }
}
