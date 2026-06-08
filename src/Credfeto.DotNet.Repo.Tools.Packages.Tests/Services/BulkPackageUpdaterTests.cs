using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog.Exceptions;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Services;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Package;
using FunFair.Test.Common;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests.Services;

public sealed class BulkPackageUpdaterTests : LoggingFolderCleanupTestBase
{
    private const string WORK_FOLDER = "/tmp/work";
    private const string REPO_URL = "git@github.com:test/test-repo.git";
    private const string TRACKING_FILE = "/tmp/tracking.json";
    private const string CACHE_FILE = "/tmp/cache.json";
    private const string TEMPLATE_REPO_URL = "git@github.com:test/template-repo.git";
    private const string PACKAGES_FILE = "/tmp/packages.json";
    private const string RELEASE_CONFIG_FILE = "/tmp/release.json";

    private static readonly DotNetVersionSettings DefaultDotNetSettings = new(
        SdkVersion: null,
        AllowPreRelease: false,
        RollForward: "latestMajor"
    );

    private static readonly ReleaseConfig DefaultReleaseConfig = new(
        AutoReleasePendingPackages: 1,
        MinimumHoursBeforeAutoRelease: 5,
        InactivityHoursBeforeAutoRelease: 9,
        NeverRelease: [],
        AllowedAutoUpgrade: [],
        AlwaysMatch: []
    );

    private readonly IBulkPackageConfigLoader _bulkPackageConfigLoader;
    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly IPackageCache _packageCache;
    private readonly IPackageUpdateConfigurationBuilder _packageUpdateConfigurationBuilder;
    private readonly IPackageUpdater _packageUpdater;
    private readonly IProjectLoader _projectLoader;
    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly IReleaseGeneration _releaseGeneration;
    private readonly IGitRepository _repoRepository;
    private readonly ISinglePackageUpdater _singlePackageUpdater;
    private readonly IGitRepository _templateRepository;
    private readonly ITrackingCache _trackingCache;
    private readonly BulkPackageUpdater _updater;

    public BulkPackageUpdaterTests(ITestOutputHelper output)
        : base(output)
    {
        this._packageUpdater = GetSubstitute<IPackageUpdater>();
        this._packageCache = GetSubstitute<IPackageCache>();
        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._globalJson = GetSubstitute<IGlobalJson>();
        this._dotNetVersion = GetSubstitute<IDotNetVersion>();
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        this._projectLoader = GetSubstitute<IProjectLoader>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();
        this._releaseConfigLoader = GetSubstitute<IReleaseConfigLoader>();
        this._releaseGeneration = GetSubstitute<IReleaseGeneration>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        this._bulkPackageConfigLoader = GetSubstitute<IBulkPackageConfigLoader>();
        this._singlePackageUpdater = GetSubstitute<ISinglePackageUpdater>();
        this._packageUpdateConfigurationBuilder = GetSubstitute<IPackageUpdateConfigurationBuilder>();
        this._templateRepository = GetSubstitute<IGitRepository>();
        this._repoRepository = GetSubstitute<IGitRepository>();

        this._updater = new BulkPackageUpdater(
            packageUpdater: this._packageUpdater,
            packageCache: this._packageCache,
            trackingCache: this._trackingCache,
            globalJson: this._globalJson,
            dotNetVersion: this._dotNetVersion,
            dotNetBuild: this._dotNetBuild,
            projectLoader: this._projectLoader,
            dotNetFilesDetector: this._dotNetFilesDetector,
            releaseConfigLoader: this._releaseConfigLoader,
            releaseGeneration: this._releaseGeneration,
            gitRepositoryFactory: this._gitRepositoryFactory,
            bulkPackageConfigLoader: this._bulkPackageConfigLoader,
            singlePackageUpdater: this._singlePackageUpdater,
            packageUpdateConfigurationBuilder: this._packageUpdateConfigurationBuilder,
            logger: this.GetTypedLogger<BulkPackageUpdater>()
        );
    }

    private static PackageUpdateContext CreateUpdateContext(string? cacheFileName = null)
    {
        return new PackageUpdateContext(
            WorkFolder: WORK_FOLDER,
            CacheFileName: cacheFileName,
            TrackingFileName: TRACKING_FILE,
            AdditionalSources: [],
            DotNetSettings: DefaultDotNetSettings,
            ReleaseConfig: DefaultReleaseConfig
        );
    }

    [Fact]
    public async Task UpdateRepositoriesWithEmptyReposCompletesWithoutExceptionAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [],
            packages: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoThrowingSolutionCheckFailedExceptionContinuesAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new SolutionCheckFailedException("Solution check failed"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoThrowingDotNetBuildErrorExceptionContinuesAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new DotNetBuildErrorException("Build failed"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoThrowingReleaseTooOldExceptionContinuesAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ReleaseTooOldException("Release too old"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoThrowingGitRepositoryLockedExceptionContinuesAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new GitRepositoryLockedException("Repo locked"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoThrowingReleaseCreatedExceptionAbortsRunAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ReleaseCreatedException("Release created"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesCallsFinallyWithCacheAndTrackingFilesAsync()
    {
        PackageUpdateContext context = new(
            WorkFolder: WORK_FOLDER,
            CacheFileName: CACHE_FILE,
            TrackingFileName: TRACKING_FILE,
            AdditionalSources: [],
            DotNetSettings: DefaultDotNetSettings,
            ReleaseConfig: DefaultReleaseConfig
        );

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new SolutionCheckFailedException("Solution check failed"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._packageCache.Received(1)
            .SaveAsync(fileName: CACHE_FILE, cancellationToken: Arg.Any<CancellationToken>());
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: TRACKING_FILE, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithNullCacheFileNameDoesNotSaveCacheAsync()
    {
        PackageUpdateContext context = CreateUpdateContext(cacheFileName: null);

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new SolutionCheckFailedException("Solution check failed"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        await this._packageCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithEmptyTrackingFileNameDoesNotSaveTrackingAsync()
    {
        PackageUpdateContext context = new(
            WorkFolder: WORK_FOLDER,
            CacheFileName: null,
            TrackingFileName: string.Empty,
            AdditionalSources: [],
            DotNetSettings: DefaultDotNetSettings,
            ReleaseConfig: DefaultReleaseConfig
        );

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: WORK_FOLDER,
                repoUrl: REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new SolutionCheckFailedException("Solution check failed"));

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [REPO_URL],
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        await this._trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithNoCachedPackagesSkipsPackageUpdaterAsync()
    {
        PackageUpdateContext context = CreateUpdateContext();

        await this._updater.UpdateRepositoriesAsync(
            updateContext: context,
            repositories: [],
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._packageUpdater.DidNotReceive()
            .UpdateAsync(
                Arg.Any<string>(),
                Arg.Any<PackageUpdateConfiguration>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task BulkUpdateWithNullCacheFileDoesNotLoadCacheAsync()
    {
        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DefaultDotNetSettings);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        this._packageCache.GetAll().Returns([]);

        await this._updater.BulkUpdateAsync(
            templateRepository: TEMPLATE_REPO_URL,
            cacheFileName: null,
            trackingFileName: string.Empty,
            packagesFileName: PACKAGES_FILE,
            workFolder: this.TempFolder,
            releaseConfigFileName: RELEASE_CONFIG_FILE,
            additionalNugetSources: [],
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._packageCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task BulkUpdateWithNonExistentCacheFileDoesNotLoadCacheAsync()
    {
        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DefaultDotNetSettings);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        this._packageCache.GetAll().Returns([]);

        string nonExistentCache = Path.Combine(this.TempFolder, "nonexistent-cache.json");

        await this._updater.BulkUpdateAsync(
            templateRepository: TEMPLATE_REPO_URL,
            cacheFileName: nonExistentCache,
            trackingFileName: string.Empty,
            packagesFileName: PACKAGES_FILE,
            workFolder: this.TempFolder,
            releaseConfigFileName: RELEASE_CONFIG_FILE,
            additionalNugetSources: [],
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._packageCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task BulkUpdateWithExistingCacheFileLoadsCacheAsync()
    {
        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DefaultDotNetSettings);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        this._packageCache.GetAll().Returns([]);

        string cacheFile = Path.Combine(this.TempFolder, "cache.json");
        await File.WriteAllTextAsync(path: cacheFile, contents: "[]", cancellationToken: this.CancellationToken());

        await this._updater.BulkUpdateAsync(
            templateRepository: TEMPLATE_REPO_URL,
            cacheFileName: cacheFile,
            trackingFileName: string.Empty,
            packagesFileName: PACKAGES_FILE,
            workFolder: this.TempFolder,
            releaseConfigFileName: RELEASE_CONFIG_FILE,
            additionalNugetSources: [],
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._packageCache.Received(1)
            .LoadAsync(fileName: cacheFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task BulkUpdateWithExistingTrackingFileLoadsTrackingAsync()
    {
        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DefaultDotNetSettings);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        this._packageCache.GetAll().Returns([]);

        string trackingFile = Path.Combine(this.TempFolder, "tracking.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._updater.BulkUpdateAsync(
            templateRepository: TEMPLATE_REPO_URL,
            cacheFileName: null,
            trackingFileName: trackingFile,
            packagesFileName: PACKAGES_FILE,
            workFolder: this.TempFolder,
            releaseConfigFileName: RELEASE_CONFIG_FILE,
            additionalNugetSources: [],
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this
            ._trackingCache.Received(1)
            .LoadAsync(fileName: trackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public Task BulkUpdateWithMissingSdkThrowsDotNetBuildErrorExceptionAsync()
    {
        DotNetVersionSettings settingsWithSdk = new(
            SdkVersion: "10.0.100",
            AllowPreRelease: false,
            RollForward: "latestMajor"
        );

        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(settingsWithSdk);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        this._packageCache.GetAll().Returns([]);

        return Assert.ThrowsAsync<DotNetBuildErrorException>(() =>
            this
                ._updater.BulkUpdateAsync(
                    templateRepository: TEMPLATE_REPO_URL,
                    cacheFileName: null,
                    trackingFileName: string.Empty,
                    packagesFileName: PACKAGES_FILE,
                    workFolder: this.TempFolder,
                    releaseConfigFileName: RELEASE_CONFIG_FILE,
                    additionalNugetSources: [],
                    repositories: [],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task BulkUpdateWithCachedPackagesUpdatesCacheAsync()
    {
        PackageUpdate packageUpdate = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([packageUpdate]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DefaultDotNetSettings);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        IReadOnlyList<PackageVersion> cachedPackages = [new PackageVersion("Test.Package", new NuGetVersion("1.0.0"))];
        this._packageCache.GetAll().Returns(cachedPackages);
        this._packageUpdateConfigurationBuilder.Build(Arg.Any<PackageUpdate>())
            .Returns(new PackageUpdateConfiguration(new PackageMatch("Test.Package", false), []));
        this._packageUpdater.UpdateAsync(
                Arg.Any<string>(),
                Arg.Any<PackageUpdateConfiguration>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([]);

        await this._updater.BulkUpdateAsync(
            templateRepository: TEMPLATE_REPO_URL,
            cacheFileName: null,
            trackingFileName: string.Empty,
            packagesFileName: PACKAGES_FILE,
            workFolder: this.TempFolder,
            releaseConfigFileName: RELEASE_CONFIG_FILE,
            additionalNugetSources: [],
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        this._packageCache.Received(1).Reset();
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoHavingNoChangelogSkipsWithLogAsync()
    {
        string repoDir = Path.Combine(this.TempFolder, "repo");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            this._repoRepository.Active.Returns(realRepo);
            this._repoRepository.WorkingDirectory.Returns(repoDir);
            this._repoRepository.ClonePath.Returns(REPO_URL);
            this._repoRepository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
            this._repoRepository.HeadRev.Returns("abc123deadbeef");

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: WORK_FOLDER,
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(this._repoRepository);

            PackageUpdateContext context = CreateUpdateContext();

            await this._updater.UpdateRepositoriesAsync(
                updateContext: context,
                repositories: [REPO_URL],
                packages: [],
                cancellationToken: this.CancellationToken()
            );

            this._trackingCache.Received(1).Set(repoUrl: REPO_URL, value: "abc123deadbeef");
        }
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoHavingChangelogButNoDotNetFilesSkipsAsync()
    {
        string repoDir = Path.Combine(this.TempFolder, "repo");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog",
            cancellationToken: this.CancellationToken()
        );

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            this._repoRepository.Active.Returns(realRepo);
            this._repoRepository.WorkingDirectory.Returns(repoDir);
            this._repoRepository.ClonePath.Returns(REPO_URL);
            this._repoRepository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
            this._repoRepository.HeadRev.Returns("abc123deadbeef");

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: WORK_FOLDER,
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(this._repoRepository);

            this._dotNetFilesDetector.FindAsync(baseFolder: repoDir, cancellationToken: Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [], Projects: []));

            this._globalJson.LoadGlobalJsonAsync(
                    baseFolder: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(DefaultDotNetSettings);

            PackageUpdateContext context = CreateUpdateContext();

            await this._updater.UpdateRepositoriesAsync(
                updateContext: context,
                repositories: [REPO_URL],
                packages: [],
                cancellationToken: this.CancellationToken()
            );

            this._trackingCache.Received(1).Set(repoUrl: REPO_URL, value: "abc123deadbeef");
        }
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoHavingDotNetFilesAndNoPackagesCallsReleaseGenerationAsync()
    {
        string repoDir = Path.Combine(this.TempFolder, "repo");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog",
            cancellationToken: this.CancellationToken()
        );

        string solutionFile = Path.Combine(repoDir, "Test.sln");
        string srcDir = Path.Combine(repoDir, "src");
        Directory.CreateDirectory(srcDir);
        string projectFile = Path.Combine(srcDir, "Test.csproj");
        await File.WriteAllTextAsync(
            path: solutionFile,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            this._repoRepository.Active.Returns(realRepo);
            this._repoRepository.WorkingDirectory.Returns(repoDir);
            this._repoRepository.ClonePath.Returns(REPO_URL);
            this._repoRepository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
            this._repoRepository.HeadRev.Returns("abc123deadbeef");

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: WORK_FOLDER,
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(this._repoRepository);

            this._dotNetFilesDetector.FindAsync(baseFolder: repoDir, cancellationToken: Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [solutionFile], Projects: [projectFile]));

            this._globalJson.LoadGlobalJsonAsync(
                    baseFolder: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(DefaultDotNetSettings);

            this._dotNetBuild.LoadBuildSettingsAsync(
                    projects: Arg.Any<IReadOnlyList<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));

            PackageUpdateContext context = CreateUpdateContext();

            await this._updater.UpdateRepositoriesAsync(
                updateContext: context,
                repositories: [REPO_URL],
                packages: [],
                cancellationToken: this.CancellationToken()
            );

            await this
                ._releaseGeneration.Received(1)
                .TryCreateNextPatchAsync(
                    repoContext: Arg.Any<Credfeto.DotNet.Repo.Tools.Models.RepoContext>(),
                    dotNetFiles: Arg.Any<DotNetFiles>(),
                    buildSettings: Arg.Any<BuildSettings>(),
                    dotNetSettings: Arg.Any<DotNetVersionSettings>(),
                    packages: Arg.Any<IReadOnlyList<PackageUpdate>>(),
                    releaseConfig: Arg.Any<ReleaseConfig>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoHavingDotNetFilesAndPackagesResetsProjectLoaderAsync()
    {
        string repoDir = Path.Combine(this.TempFolder, "repo");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog",
            cancellationToken: this.CancellationToken()
        );

        string solutionFile = Path.Combine(repoDir, "Test.sln");
        string srcDir = Path.Combine(repoDir, "src");
        Directory.CreateDirectory(srcDir);
        string projectFile = Path.Combine(srcDir, "Test.csproj");
        await File.WriteAllTextAsync(
            path: solutionFile,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );

        PackageUpdate packageUpdate = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            this._repoRepository.Active.Returns(realRepo);
            this._repoRepository.WorkingDirectory.Returns(repoDir);
            this._repoRepository.ClonePath.Returns(REPO_URL);
            this._repoRepository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
            this._repoRepository.HeadRev.Returns("abc123deadbeef");

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: WORK_FOLDER,
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(this._repoRepository);

            this._dotNetFilesDetector.FindAsync(baseFolder: repoDir, cancellationToken: Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [solutionFile], Projects: [projectFile]));

            this._globalJson.LoadGlobalJsonAsync(
                    baseFolder: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(DefaultDotNetSettings);

            this._dotNetBuild.LoadBuildSettingsAsync(
                    projects: Arg.Any<IReadOnlyList<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));

            this._singlePackageUpdater.UpdateAsync(
                    updateContext: Arg.Any<PackageUpdateContext>(),
                    repoContext: Arg.Any<Credfeto.DotNet.Repo.Tools.Models.RepoContext>(),
                    solutions: Arg.Any<IReadOnlyList<string>>(),
                    sourceDirectory: Arg.Any<string>(),
                    buildSettings: Arg.Any<BuildSettings>(),
                    dotNetSettings: Arg.Any<DotNetVersionSettings>(),
                    package: Arg.Any<PackageUpdate>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(false);

            PackageUpdateContext context = CreateUpdateContext();

            await this._updater.UpdateRepositoriesAsync(
                updateContext: context,
                repositories: [REPO_URL],
                packages: [packageUpdate],
                cancellationToken: this.CancellationToken()
            );

            this._projectLoader.Received(1).Reset();
        }
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task UpdateRepositoriesWithRepoHavingDotNetFilesAndPackageUpdateReturnsTrueDoesNotCallReleaseGenerationAsync()
    {
        string repoDir = Path.Combine(this.TempFolder, "repo");
        Directory.CreateDirectory(repoDir);
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog",
            cancellationToken: this.CancellationToken()
        );

        string solutionFile = Path.Combine(repoDir, "Test.sln");
        string srcDir = Path.Combine(repoDir, "src");
        Directory.CreateDirectory(srcDir);
        string projectFile = Path.Combine(srcDir, "Test.csproj");
        await File.WriteAllTextAsync(
            path: solutionFile,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );
        await File.WriteAllTextAsync(
            path: projectFile,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );

        PackageUpdate packageUpdate = new(
            packageId: "Test.Package",
            packageType: "nuget",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            this._repoRepository.Active.Returns(realRepo);
            this._repoRepository.WorkingDirectory.Returns(repoDir);
            this._repoRepository.ClonePath.Returns(REPO_URL);
            this._repoRepository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
            this._repoRepository.HeadRev.Returns("abc123deadbeef");

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: WORK_FOLDER,
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(this._repoRepository);

            this._dotNetFilesDetector.FindAsync(baseFolder: repoDir, cancellationToken: Arg.Any<CancellationToken>())
                .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [solutionFile], Projects: [projectFile]));

            this._globalJson.LoadGlobalJsonAsync(
                    baseFolder: Arg.Any<string>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(DefaultDotNetSettings);

            this._dotNetBuild.LoadBuildSettingsAsync(
                    projects: Arg.Any<IReadOnlyList<string>>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));

            this._singlePackageUpdater.UpdateAsync(
                    updateContext: Arg.Any<PackageUpdateContext>(),
                    repoContext: Arg.Any<Credfeto.DotNet.Repo.Tools.Models.RepoContext>(),
                    solutions: Arg.Any<IReadOnlyList<string>>(),
                    sourceDirectory: Arg.Any<string>(),
                    buildSettings: Arg.Any<BuildSettings>(),
                    dotNetSettings: Arg.Any<DotNetVersionSettings>(),
                    package: Arg.Any<PackageUpdate>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(true);

            PackageUpdateContext context = CreateUpdateContext();

            await this._updater.UpdateRepositoriesAsync(
                updateContext: context,
                repositories: [REPO_URL],
                packages: [packageUpdate],
                cancellationToken: this.CancellationToken()
            );

            await this
                ._releaseGeneration.DidNotReceive()
                .TryCreateNextPatchAsync(
                    repoContext: Arg.Any<Credfeto.DotNet.Repo.Tools.Models.RepoContext>(),
                    dotNetFiles: Arg.Any<DotNetFiles>(),
                    buildSettings: Arg.Any<BuildSettings>(),
                    dotNetSettings: Arg.Any<DotNetVersionSettings>(),
                    packages: Arg.Any<IReadOnlyList<PackageUpdate>>(),
                    releaseConfig: Arg.Any<ReleaseConfig>(),
                    cancellationToken: Arg.Any<CancellationToken>()
                );
        }
    }

    [Fact]
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Unit Test"
    )]
    public async Task BulkUpdateWithNonExistentTrackingFileDoesNotLoadTrackingAsync()
    {
        this._templateRepository.WorkingDirectory.Returns("/template");
        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: TEMPLATE_REPO_URL,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(this._templateRepository);
        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DefaultDotNetSettings);
        IReadOnlyList<System.Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(Arg.Any<CancellationToken>()).Returns(noSdks);
        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(DefaultReleaseConfig);
        this._packageCache.GetAll().Returns([]);

        string nonExistentTracking = Path.Combine(this.TempFolder, "nonexistent-tracking.json");

        await this._updater.BulkUpdateAsync(
            templateRepository: TEMPLATE_REPO_URL,
            cacheFileName: null,
            trackingFileName: nonExistentTracking,
            packagesFileName: PACKAGES_FILE,
            workFolder: this.TempFolder,
            releaseConfigFileName: RELEASE_CONFIG_FILE,
            additionalNugetSources: [],
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._trackingCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
