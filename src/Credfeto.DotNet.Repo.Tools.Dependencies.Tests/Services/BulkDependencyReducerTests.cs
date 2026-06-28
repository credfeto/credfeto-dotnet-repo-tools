using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Services;

public sealed class BulkDependencyReducerTests : LoggingFolderCleanupTestBase
{
    private static readonly DotNetVersionSettings DotNetSettings = new(
        SdkVersion: null,
        AllowPreRelease: false,
        RollForward: "major"
    );

    private readonly IDependencyReducer _dependencyReducer;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly IBulkDependencyReducer _sut;
    private readonly ITrackingCache _trackingCache;
    private readonly ITrackingHashGenerator _trackingHashGenerator;

    public BulkDependencyReducerTests(ITestOutputHelper output)
        : base(output)
    {
        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        this._globalJson = GetSubstitute<IGlobalJson>();
        this._dotNetVersion = GetSubstitute<IDotNetVersion>();
        this._dependencyReducer = GetSubstitute<IDependencyReducer>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();
        this._trackingHashGenerator = GetSubstitute<ITrackingHashGenerator>();

        MockIGlobalJsonLoadGlobalJson(this._globalJson, DotNetSettings);
        MockIDotNetVersionGetInstalledSdks(this._dotNetVersion, []);
        MockIDependencyReducerCheckReferences(this._dependencyReducer, false);

        this._sut = new BulkDependencyReducer(
            trackingCache: this._trackingCache,
            globalJson: this._globalJson,
            dotNetVersion: this._dotNetVersion,
            gitRepositoryFactory: this._gitRepositoryFactory,
            dependencyReducer: this._dependencyReducer,
            trackingHashGenerator: this._trackingHashGenerator,
            dotNetFilesDetector: this._dotNetFilesDetector,
            logger: this.GetTypedLogger<BulkDependencyReducer>()
        );
    }

    private static void MockIGitRepositoryFactoryOpenOrClone(
        IGitRepositoryFactory factory,
        IGitRepository templateRepo,
        string workingDirectory
    )
    {
        templateRepo.WorkingDirectory.Returns(workingDirectory);
        factory
            .OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);
    }

    private static void MockIGitRepositoryFactoryOpenOrClone<TException>(
        IGitRepositoryFactory factory,
        IGitRepository templateRepo,
        string workingDirectory,
        TException ex
    )
        where TException : Exception
    {
        MockIGitRepositoryFactoryOpenOrClone(
            factory: factory,
            templateRepo: templateRepo,
            workingDirectory: workingDirectory
        );

        int callCount = 0;
        factory
            .When(f => f.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                ++callCount;

                if (callCount > 1)
                {
                    throw ex;
                }
            });
    }

    private static void MockIGitRepositoryFactoryOpenOrClone(
        IGitRepositoryFactory factory,
        IGitRepository templateRepo,
        IGitRepository testRepo,
        string workingDirectory
    )
    {
        templateRepo.WorkingDirectory.Returns(workingDirectory);
        factory
            .OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo, testRepo);
    }

    private static void MockIGitRepositoryActive(IGitRepository repo, LibGit2Sharp.Repository activeRepo)
    {
        repo.Active.Returns(activeRepo);
    }

    private static void MockIGitRepositoryClonePath(IGitRepository repo, string clonePath)
    {
        repo.ClonePath.Returns(clonePath);
    }

    private static void MockIGitRepositoryGetDefaultBranch(IGitRepository repo, string branch)
    {
        repo.GetDefaultBranch(GitConstants.Upstream).Returns(branch);
    }

    private static void MockIGitRepositoryWorkingDirectory(IGitRepository repo, string workingDirectory)
    {
        repo.WorkingDirectory.Returns(workingDirectory);
    }

    private ValueTask RunBulkUpdateWithTrackingAsync(string repoUrl, string trackingFileName)
    {
        return this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFileName,
            workFolder: this.TempFolder,
            repositories: [repoUrl],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public ValueTask BulkUpdateAsyncWithNoRepositoriesShouldSucceedAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder
        );

        return this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            workFolder: this.TempFolder,
            repositories: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldLoadAndSaveTrackingCacheWhenFileExistsAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            workFolder: this.TempFolder,
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._trackingCache.Received(1)
            .LoadAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldNotLoadTrackingCacheWhenFileDoesNotExistAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "nonexistent-tracking.json");

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            workFolder: this.TempFolder,
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._trackingCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldThrowWhenSdkVersionNotInstalledAsync()
    {
        MockIGlobalJsonLoadGlobalJson(
            this._globalJson,
            new DotNetVersionSettings(SdkVersion: "99.0.0", AllowPreRelease: false, RollForward: "major")
        );
        MockIDotNetVersionGetInstalledSdks(this._dotNetVersion, [new System.Version(10, 0, 0)]);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder
        );

        await Assert.ThrowsAsync<DotNetBuildErrorException>(() =>
            this
                ._sut.BulkUpdateAsync(
                    templateRepository: "https://github.com/template/repo.git",
                    trackingFileName: string.Empty,
                    workFolder: this.TempFolder,
                    repositories: [],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact]
    public ValueTask BulkUpdateAsyncShouldHandleGitRepositoryLockedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder,
            ex: new GitRepositoryLockedException("Repository is locked")
        );

        return this.RunBulkUpdateWithTrackingAsync("https://github.com/test/locked-repo.git", string.Empty);
    }

    [Fact]
    public ValueTask BulkUpdateAsyncShouldHandleSolutionCheckFailedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder,
            ex: new SolutionCheckFailedException("Solution check failed")
        );

        return this.RunBulkUpdateWithTrackingAsync("https://github.com/test/failing-solution-repo.git", string.Empty);
    }

    [Fact]
    public ValueTask BulkUpdateAsyncShouldHandleDotNetBuildErrorExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder,
            ex: new DotNetBuildErrorException("Build failed")
        );

        return this.RunBulkUpdateWithTrackingAsync("https://github.com/test/build-error-repo.git", string.Empty);
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldSaveTrackingFileAfterLockedRepoExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            workingDirectory: this.TempFolder,
            ex: new GitRepositoryLockedException("Repository is locked")
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking-for-exception.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            workFolder: this.TempFolder,
            repositories: ["https://github.com/test/locked-repo-2.git"],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._trackingCache.Received(2)
            .SaveAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldProcessRepoWithNoChangelogAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-nochangelog");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, "https://github.com/test/noop-repo.git");
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        await this.RunBulkUpdateWithTrackingAsync("https://github.com/test/noop-repo.git", string.Empty);
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldProcessRepoWithChangelogButNoDotNetFilesAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-changelog-nodotnet");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        MockIDotNetFilesDetectorFind(
            this._dotNetFilesDetector,
            new DotNetFiles(SourceDirectory: repoDir, Solutions: [], Projects: [])
        );

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, "https://github.com/test/changelog-nodotnet-repo.git");
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        await this.RunBulkUpdateWithTrackingAsync("https://github.com/test/changelog-nodotnet-repo.git", string.Empty);

        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldProcessRepoWithChangelogAndDotNetFilesAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-dotnet");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "Test.csproj");
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        MockIDotNetFilesDetectorFind(
            this._dotNetFilesDetector,
            new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile])
        );

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, "https://github.com/test/dotnet-repo.git");
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        await this.RunBulkUpdateWithTrackingAsync("https://github.com/test/dotnet-repo.git", string.Empty);

        await this
            ._dependencyReducer.Received(1)
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>());
        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldSkipProcessingWhenTrackingShowsNoChangesAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-tracking-match");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        const string cloneUrl = "https://github.com/test/tracking-match-repo.git";
        const string existingHash = "abc123def456";

        MockITrackingCacheGet(this._trackingCache, cloneUrl, existingHash);
        MockITrackingHashGeneratorGenerateTrackingHash(this._trackingHashGenerator, existingHash);

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking-match.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, cloneUrl);
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        await this.RunBulkUpdateWithTrackingAsync(repoUrl: cloneUrl, trackingFileName: trackingFile);

        await this
            ._dependencyReducer.DidNotReceive()
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>());
        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldProcessWhenPreviousTrackingHashIsNullAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-tracking-null");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "TrackingNull.csproj");
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        const string cloneUrl = "https://github.com/test/tracking-null-repo.git";
        MockITrackingCacheGet(this._trackingCache, cloneUrl, null);

        MockIDotNetFilesDetectorFind(
            this._dotNetFilesDetector,
            new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile])
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking-null.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, cloneUrl);
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        await this.RunBulkUpdateWithTrackingAsync(repoUrl: cloneUrl, trackingFileName: trackingFile);

        await this
            ._dependencyReducer.Received(1)
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>());
        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldProcessWhenTrackingHashDiffersAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-tracking-differ");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "TrackingDiffer.csproj");
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        const string cloneUrl = "https://github.com/test/tracking-differ-repo.git";
        MockITrackingCacheGet(this._trackingCache, cloneUrl, "old-hash-123");
        MockITrackingHashGeneratorGenerateTrackingHash(this._trackingHashGenerator, "new-hash-456");

        MockIDotNetFilesDetectorFind(
            this._dotNetFilesDetector,
            new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile])
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "tracking-differ.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, cloneUrl);
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        await this.RunBulkUpdateWithTrackingAsync(repoUrl: cloneUrl, trackingFileName: trackingFile);

        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldSkipUpdateTrackingCacheWhenTrackingFileNameIsNonEmptyAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-commit-with-tracking");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "CommitTracking.csproj");
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        const string cloneUrl = "https://github.com/test/commit-tracking-repo.git";
        MockITrackingCacheGet(this._trackingCache, cloneUrl, null);

        MockIDotNetFilesDetectorFind(
            this._dotNetFilesDetector,
            new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile])
        );

        string trackingFile = Path.Combine(path1: this.TempFolder, path2: "commit-tracking.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, cloneUrl);
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        IBulkDependencyReducer sut = this.BuildSutWithCallbackReducer();

        await sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: trackingFile,
            workFolder: this.TempFolder,
            repositories: [cloneUrl],
            cancellationToken: this.CancellationToken()
        );

        await testRepo
            .Received(1)
            .CommitNamedAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        this._trackingCache.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async ValueTask BulkUpdateAsyncShouldInvokeCommitAsyncWhenCheckReferencesInvokesCallbackAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-commit-callback");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog\n",
            cancellationToken: this.CancellationToken()
        );

        string projectFile = Path.Combine(repoDir, "Commit.csproj");
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            cancellationToken: this.CancellationToken()
        );

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        MockIDotNetFilesDetectorFind(
            this._dotNetFilesDetector,
            new DotNetFiles(SourceDirectory: repoDir, Solutions: [projectFile], Projects: [projectFile])
        );
        MockITrackingHashGeneratorGenerateTrackingHash(this._trackingHashGenerator, "hash-for-commit-test");

        const string cloneUrl = "https://github.com/test/commit-callback-repo.git";

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        MockIGitRepositoryActive(testRepo, activeRepo);
        MockIGitRepositoryClonePath(testRepo, cloneUrl);
        MockIGitRepositoryWorkingDirectory(testRepo, repoDir);
        MockIGitRepositoryGetDefaultBranch(testRepo, "main");

        MockIGitRepositoryFactoryOpenOrClone(
            factory: this._gitRepositoryFactory,
            templateRepo: templateRepo,
            testRepo: testRepo,
            workingDirectory: this.TempFolder
        );

        IBulkDependencyReducer sut = this.BuildSutWithCallbackReducer();

        await sut.BulkUpdateAsync(
            templateRepository: "https://github.com/template/repo.git",
            trackingFileName: string.Empty,
            workFolder: this.TempFolder,
            repositories: [cloneUrl],
            cancellationToken: this.CancellationToken()
        );

        await testRepo
            .Received(1)
            .CommitNamedAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await testRepo.Received(1).PushAsync(Arg.Any<CancellationToken>());
    }

    private IBulkDependencyReducer BuildSutWithCallbackReducer()
    {
        IDependencyReducer callbackReducer = GetSubstitute<IDependencyReducer>();
        MockIDependencyReducerCheckReferencesWithCallback(callbackReducer);

        return new BulkDependencyReducer(
            trackingCache: this._trackingCache,
            globalJson: this._globalJson,
            dotNetVersion: this._dotNetVersion,
            gitRepositoryFactory: this._gitRepositoryFactory,
            dependencyReducer: callbackReducer,
            trackingHashGenerator: this._trackingHashGenerator,
            dotNetFilesDetector: this._dotNetFilesDetector,
            logger: this.GetTypedLogger<BulkDependencyReducer>()
        );
    }

    private static void MockIDependencyReducerCheckReferencesWithCallback(IDependencyReducer dependencyReducer)
    {
        dependencyReducer
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
                ci.Arg<ReferenceConfig>()
                    .OnSuccessfulRemoval("Test.csproj", "Removed redundant dependency", ci.Arg<CancellationToken>())
                    .AsTask()
                    .IsCompletedSuccessfully
            );
    }

    private static void MockIGlobalJsonLoadGlobalJson(IGlobalJson globalJson, in DotNetVersionSettings value)
    {
        globalJson.LoadGlobalJsonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(value);
    }

    private static void MockIDotNetVersionGetInstalledSdks(
        IDotNetVersion dotNetVersion,
        IReadOnlyList<System.Version> value
    )
    {
        dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(value);
    }

    private static void MockIDependencyReducerCheckReferences(IDependencyReducer dependencyReducer, bool value)
    {
        dependencyReducer
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>())
            .Returns(value);
    }

    private static void MockIDotNetFilesDetectorFind(IDotNetFilesDetector detector, in DotNetFiles result)
    {
        detector.FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
    }

    private static void MockITrackingHashGeneratorGenerateTrackingHash(ITrackingHashGenerator generator, string hash)
    {
        generator.GenerateTrackingHashAsync(Arg.Any<RepoContext>(), Arg.Any<CancellationToken>()).Returns(hash);
    }

    private static void MockITrackingCacheGet(ITrackingCache cache, string cloneUrl, string? hash)
    {
        cache.Get(cloneUrl).Returns(hash);
    }
}
