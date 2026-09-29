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

    private string CreateWorkDir()
    {
        string workDir = Path.Combine(this.TempFolder, Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        return workDir;
    }

    private static RepoContext CreateRepoContext(string workDir)
    {
        return new(
            ClonePath: workDir,
            Repository: GetSubstitute<IGitRepository>(),
            WorkingDirectory: workDir,
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );
    }

    private Task WriteFileAsync(string directory, string fileName, string contents)
    {
        return File.WriteAllTextAsync(
            path: Path.Combine(path1: directory, path2: fileName),
            contents: contents,
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async Task GenerateTrackingHashAsyncWithEmptyDirectoryReturnsNonEmptyStringAsync()
    {
        // An empty working directory contains no files matching the masks.
        // The hash must still be returned as a non-null, non-empty base64 string.
        string emptyDir = this.CreateWorkDir();

        RepoContext repoContext = CreateRepoContext(emptyDir);

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
        string emptyDir = this.CreateWorkDir();

        string populatedDir = this.CreateWorkDir();

        await this.WriteFileAsync(directory: populatedDir, fileName: "test.sln", contents: "# solution file");

        await this.WriteFileAsync(directory: populatedDir, fileName: "test.csproj", contents: "<Project />");

        await this.WriteFileAsync(directory: populatedDir, fileName: "test.props", contents: "<Project />");

        RepoContext emptyContext = CreateRepoContext(emptyDir);

        RepoContext populatedContext = CreateRepoContext(populatedDir);

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
        string workDir = this.CreateWorkDir();

        await this.WriteFileAsync(directory: workDir, fileName: "test.sln", contents: "# solution file");

        await this.WriteFileAsync(directory: workDir, fileName: "test.csproj", contents: "<Project />");

        RepoContext repoContext = CreateRepoContext(workDir);

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

    [Theory]
    [InlineData("obj", "test.csproj.nuget.g.props")]
    [InlineData("bin", "whatever.props")]
    [InlineData(".git", "some.props")]
    [InlineData("src/Example/obj", "Example.csproj.nuget.g.props")]
    [InlineData("src/Example/bin", "Example.props")]
    public async Task GenerateTrackingHashAsyncIgnoresFilesInExcludedDirectoriesAsync(
        string relativeDirectory,
        string fileName
    )
    {
        // Files under obj/, bin/ and .git/ are generated or metadata content that varies
        // between a freshly built tree and a freshly cleaned tree for identical source,
        // so they must not influence the tracking hash. The nested rows cover the real-world
        // per-project src/<Project>/obj and src/<Project>/bin layout, so exclusion must apply
        // at every level of the directory walk, not just at the working-directory root.
        string workDir = this.CreateWorkDir();

        await this.WriteFileAsync(directory: workDir, fileName: "test.csproj", contents: "<Project />");

        RepoContext repoContext = CreateRepoContext(workDir);

        string hashBeforeGeneratedFiles = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        string excludedDirectory = Path.Combine(path1: workDir, path2: relativeDirectory);
        Directory.CreateDirectory(excludedDirectory);

        await this.WriteFileAsync(directory: excludedDirectory, fileName: fileName, contents: "<Project />");

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
        string workDir = this.CreateWorkDir();

        await this.WriteFileAsync(directory: workDir, fileName: "test.props", contents: "<Project />");

        RepoContext repoContext = CreateRepoContext(workDir);

        string firstHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        await this.WriteFileAsync(
            directory: workDir,
            fileName: "test.props",
            contents: "<Project><PropertyGroup /></Project>"
        );

        string secondHash = await this._hashGenerator.GenerateTrackingHashAsync(
            repoContext: repoContext,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEqual(expected: firstHash, actual: secondHash, comparer: StringComparer.Ordinal);
    }
}
