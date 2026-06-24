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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute setup"
    )]
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

        this._globalJson.LoadGlobalJsonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(DotNetSettings);
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<System.Version>>([]);
        this._dependencyReducer.CheckReferencesAsync(
                Arg.Any<DotNetFiles>(),
                Arg.Any<ReferenceConfig>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

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

    private void SetupTemplateRepo(IGitRepository templateRepo)
    {
        templateRepo.WorkingDirectory.Returns(this.TempFolder);
        this._gitRepositoryFactory.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);
    }

    private void SetupTemplateRepoThrowingOnSubsequentCalls<TException>(IGitRepository templateRepo, TException ex)
        where TException : Exception
    {
        templateRepo.WorkingDirectory.Returns(this.TempFolder);
        this._gitRepositoryFactory.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);

        int callCount = 0;
        this._gitRepositoryFactory.When(f =>
                f.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            )
            .Do(_ =>
            {
                ++callCount;

                if (callCount > 1)
                {
                    throw ex;
                }
            });
    }

    private void SetupTwoRepos(IGitRepository templateRepo, IGitRepository testRepo)
    {
        templateRepo.WorkingDirectory.Returns(this.TempFolder);
        this._gitRepositoryFactory.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(templateRepo);

        int callCount = 0;
        this._gitRepositoryFactory.When(f =>
                f.OpenOrCloneAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            )
            .Do(_ =>
            {
                ++callCount;

                if (callCount == 2)
                {
                    this._gitRepositoryFactory.OpenOrCloneAsync(
                            Arg.Any<string>(),
                            Arg.Any<string>(),
                            Arg.Any<CancellationToken>()
                        )
                        .Returns(testRepo);
                }
            });
    }

    private Task RunBulkUpdateWithTrackingAsync(string repoUrl, string trackingFileName)
    {
        return this
            ._sut.BulkUpdateAsync(
                templateRepository: "https://github.com/template/repo.git",
                trackingFileName: trackingFileName,
                workFolder: this.TempFolder,
                repositories: [repoUrl],
                cancellationToken: this.CancellationToken()
            )
            .AsTask();
    }

    [Fact]
    public Task BulkUpdateAsyncWithNoRepositoriesShouldSucceedAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

        return this
            ._sut.BulkUpdateAsync(
                templateRepository: "https://github.com/template/repo.git",
                trackingFileName: string.Empty,
                workFolder: this.TempFolder,
                repositories: [],
                cancellationToken: this.CancellationToken()
            )
            .AsTask();
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldLoadAndSaveTrackingCacheWhenFileExistsAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

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
    public async Task BulkUpdateAsyncShouldNotLoadTrackingCacheWhenFileDoesNotExistAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute setup"
    )]
    [Fact]
    public Task BulkUpdateAsyncShouldThrowWhenSdkVersionNotInstalledAsync()
    {
        this._globalJson.LoadGlobalJsonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DotNetVersionSettings(SdkVersion: "99.0.0", AllowPreRelease: false, RollForward: "major"));
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<System.Version>>([new System.Version(10, 0, 0)]);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepo(templateRepo);

        return Assert.ThrowsAsync<DotNetBuildErrorException>(() =>
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
    public Task BulkUpdateAsyncShouldHandleGitRepositoryLockedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new GitRepositoryLockedException("Repository is locked")
        );

        return this.RunBulkUpdateWithTrackingAsync("https://github.com/test/locked-repo.git", string.Empty);
    }

    [Fact]
    public Task BulkUpdateAsyncShouldHandleSolutionCheckFailedExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new SolutionCheckFailedException("Solution check failed")
        );

        return this.RunBulkUpdateWithTrackingAsync("https://github.com/test/failing-solution-repo.git", string.Empty);
    }

    [Fact]
    public Task BulkUpdateAsyncShouldHandleDotNetBuildErrorExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new DotNetBuildErrorException("Build failed")
        );

        return this.RunBulkUpdateWithTrackingAsync("https://github.com/test/build-error-repo.git", string.Empty);
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldSaveTrackingFileAfterLockedRepoExceptionAsync()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        this.SetupTemplateRepoThrowingOnSubsequentCalls(
            templateRepo: templateRepo,
            new GitRepositoryLockedException("Repository is locked")
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
    public async Task BulkUpdateAsyncShouldProcessRepoWithNoChangelogAsync()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "gitrepo-nochangelog");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);

        using LibGit2Sharp.Repository activeRepo = new(repoDir);

        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        IGitRepository testRepo = GetSubstitute<IGitRepository>();
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/noop-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this.RunBulkUpdateWithTrackingAsync("https://github.com/test/noop-repo.git", string.Empty);
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessRepoWithChangelogButNoDotNetFilesAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/changelog-nodotnet-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this.RunBulkUpdateWithTrackingAsync("https://github.com/test/changelog-nodotnet-repo.git", string.Empty);

        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessRepoWithChangelogAndDotNetFilesAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns("https://github.com/test/dotnet-repo.git");
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this.RunBulkUpdateWithTrackingAsync("https://github.com/test/dotnet-repo.git", string.Empty);

        await this
            ._dependencyReducer.Received(1)
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>());
        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldSkipProcessingWhenTrackingShowsNoChangesAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns(cloneUrl);
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this.RunBulkUpdateWithTrackingAsync(repoUrl: cloneUrl, trackingFileName: trackingFile);

        await this
            ._dependencyReducer.DidNotReceive()
            .CheckReferencesAsync(Arg.Any<DotNetFiles>(), Arg.Any<ReferenceConfig>(), Arg.Any<CancellationToken>());
        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessWhenPreviousTrackingHashIsNullAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns(cloneUrl);
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this.RunBulkUpdateWithTrackingAsync(repoUrl: cloneUrl, trackingFileName: trackingFile);

        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldProcessWhenTrackingHashDiffersAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns(cloneUrl);
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

        await this.RunBulkUpdateWithTrackingAsync(repoUrl: cloneUrl, trackingFileName: trackingFile);

        await testRepo
            .Received(1)
            .ResetToDefaultBranchAsync(upstream: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldSkipUpdateTrackingCacheWhenTrackingFileNameIsNonEmptyAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns(cloneUrl);
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

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
    }

    [Fact]
    public async Task BulkUpdateAsyncShouldInvokeCommitAsyncWhenCheckReferencesInvokesCallbackAsync()
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
        testRepo.Active.Returns(activeRepo);
        testRepo.ClonePath.Returns(cloneUrl);
        testRepo.WorkingDirectory.Returns(repoDir);
        testRepo.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        this.SetupTwoRepos(templateRepo: templateRepo, testRepo: testRepo);

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
        return new BulkDependencyReducer(
            trackingCache: this._trackingCache,
            globalJson: this._globalJson,
            dotNetVersion: this._dotNetVersion,
            gitRepositoryFactory: this._gitRepositoryFactory,
            dependencyReducer: new CallbackInvokingDependencyReducer(),
            trackingHashGenerator: this._trackingHashGenerator,
            dotNetFilesDetector: this._dotNetFilesDetector,
            logger: this.GetTypedLogger<BulkDependencyReducer>()
        );
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

    private sealed class CallbackInvokingDependencyReducer : IDependencyReducer
    {
        public async ValueTask<bool> CheckReferencesAsync(
            DotNetFiles dotNetFiles,
            ReferenceConfig config,
            CancellationToken cancellationToken
        )
        {
            await config.OnSuccessfulRemoval("Test.csproj", "Removed redundant dependency", cancellationToken);

            return true;
        }
    }
}
