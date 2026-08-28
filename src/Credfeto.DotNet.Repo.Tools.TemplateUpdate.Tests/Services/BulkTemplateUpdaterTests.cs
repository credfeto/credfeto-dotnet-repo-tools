using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.ChangeLog.Services;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class BulkTemplateUpdaterTests : TestBase, IDisposable
{
    private const string REPO_URL = "git@github.com:test/test-repo.git";

    private readonly string _tempFolder;
    private readonly IBulkTemplateUpdater _bulkTemplateUpdater;

    private readonly ITrackingCache _trackingCache;
    private readonly ITemplateConfigLoader _templateConfigLoader;
    private readonly IGlobalJson _globalJson;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IDependaBotConfigBuilder _dependaBotConfigBuilder;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;

    public BulkTemplateUpdaterTests()
    {
        this._tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._tempFolder);

        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._templateConfigLoader = GetSubstitute<ITemplateConfigLoader>();
        this._globalJson = GetSubstitute<IGlobalJson>();
        this._dotNetVersion = GetSubstitute<IDotNetVersion>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();
        this._dependaBotConfigBuilder = GetSubstitute<IDependaBotConfigBuilder>();

        IBulkPackageConfigLoader bulkPackageConfigLoader = GetSubstitute<IBulkPackageConfigLoader>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        IReleaseConfigLoader releaseConfigLoader = GetSubstitute<IReleaseConfigLoader>();

        ServiceProvider changeLogServices = new ServiceCollection().AddChangeLog().BuildServiceProvider();
        IChangeLogUpdater changeLogUpdater = changeLogServices.GetRequiredService<IChangeLogUpdater>();
        IChangeLogLanguageFactory changeLogLanguageFactory =
            changeLogServices.GetRequiredService<IChangeLogLanguageFactory>();

        this._bulkTemplateUpdater = new BulkTemplateUpdater(
            trackingCache: this._trackingCache,
            globalJson: this._globalJson,
            dotNetFilesDetector: this._dotNetFilesDetector,
            dotNetVersion: this._dotNetVersion,
            dotNetSolutionCheck: GetSubstitute<IDotNetSolutionCheck>(),
            dotNetBuild: GetSubstitute<IDotNetBuild>(),
            releaseConfigLoader: releaseConfigLoader,
            releaseGeneration: GetSubstitute<IReleaseGeneration>(),
            gitRepositoryFactory: this._gitRepositoryFactory,
            bulkPackageConfigLoader: bulkPackageConfigLoader,
            fileUpdater: GetSubstitute<IFileUpdater>(),
            dependaBotConfigBuilder: this._dependaBotConfigBuilder,
            labelsBuilder: GetSubstitute<ILabelsBuilder>(),
            templateConfigLoader: this._templateConfigLoader,
            changeLogDetector: new ChangeLogDetector(),
            changeLogUpdater: changeLogUpdater,
            changeLogLanguageFactory: changeLogLanguageFactory,
            logger: GetSubstitute<ILogger<BulkTemplateUpdater>>()
        );

        this.SetupDefaultMocks(
            gitRepositoryFactory: this._gitRepositoryFactory,
            bulkPackageConfigLoader: bulkPackageConfigLoader,
            releaseConfigLoader: releaseConfigLoader
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempFolder))
        {
            Directory.Delete(path: this._tempFolder, recursive: true);
        }
    }

    private void SetupDefaultMocks(
        IGitRepositoryFactory gitRepositoryFactory,
        IBulkPackageConfigLoader bulkPackageConfigLoader,
        IReleaseConfigLoader releaseConfigLoader
    )
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        templateRepo.WorkingDirectory.Returns(this._tempFolder);

        gitRepositoryFactory
            .OpenOrCloneAsync(
                workDir: Arg.Any<string>(),
                repoUrl: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(templateRepo);

        bulkPackageConfigLoader
            .LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns([]);

        this._templateConfigLoader.LoadConfigAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(EmptyTemplateConfig());

        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new DotNetVersionSettings(SdkVersion: null, AllowPreRelease: false, RollForward: "latestMajor"));

        IReadOnlyList<Version> noInstalledSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(noInstalledSdks);

        releaseConfigLoader
            .LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(
                new ReleaseConfig(
                    AutoReleasePendingPackages: 0,
                    MinimumHoursBeforeAutoRelease: 0,
                    InactivityHoursBeforeAutoRelease: 0,
                    NeverRelease: [],
                    AllowedAutoUpgrade: [],
                    AlwaysMatch: []
                )
            );
    }

    private static TemplateConfig EmptyTemplateConfig()
    {
        return new TemplateConfig(
            general: new GeneralTemplateConfig(files: []),
            gitHub: new GitHubTemplateConfig(
                issueTemplates: false,
                pullRequestTemplates: false,
                actions: false,
                linters: false,
                files: [],
                dependabot: new DependabotTemplateConfig(generate: false),
                labels: new LabelsTemplateConfig(generate: false)
            ),
            dotNet: new DotnetTemplateConfig(globalJson: false, jetBrainsDotSettings: false, files: []),
            cleanup: new CleanupTemplateConfig(files: [])
        );
    }

    [Fact]
    public async Task BulkUpdateWithNoTrackingFileAndNoRepositoriesCompletesSuccessfully()
    {
        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: "git@github.com:template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: "/packages.json",
            workFolder: this._tempFolder,
            templateConfigFileName: "/template.json",
            releaseConfigFileName: "/release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._trackingCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._trackingCache.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateWithNonexistentTrackingFileSkipsLoadButDoesSave()
    {
        string nonexistentTrackingFile = Path.Combine(this._tempFolder, "nonexistent-tracking.json");

        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: "git@github.com:template/repo.git",
            trackingFileName: nonexistentTrackingFile,
            packagesFileName: "/packages.json",
            workFolder: this._tempFolder,
            templateConfigFileName: "/template.json",
            releaseConfigFileName: "/release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._trackingCache.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this
            ._trackingCache.Received(1)
            .SaveAsync(fileName: nonexistentTrackingFile, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateWithExistingTrackingFileLoadsAndSavesTracking()
    {
        string trackingFile = Path.Combine(this._tempFolder, "tracking.json");
        await File.WriteAllTextAsync(path: trackingFile, contents: "{}", cancellationToken: this.CancellationToken());

        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: "git@github.com:template/repo.git",
            trackingFileName: trackingFile,
            packagesFileName: "/packages.json",
            workFolder: this._tempFolder,
            templateConfigFileName: "/template.json",
            releaseConfigFileName: "/release.json",
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
    public async Task BulkUpdateWithSdkVersionNullCompletesSuccessfully()
    {
        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: "git@github.com:template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: "/packages.json",
            workFolder: this._tempFolder,
            templateConfigFileName: "/template.json",
            releaseConfigFileName: "/release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._dotNetVersion.Received(1).GetInstalledSdksAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BulkUpdateWithInstalledSdkVersionCompletesSuccessfully()
    {
        Version sdkVersion = new(9, 0, 300);

        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(
                new DotNetVersionSettings(
                    SdkVersion: sdkVersion.ToString(),
                    AllowPreRelease: false,
                    RollForward: "latestMajor"
                )
            );

        IReadOnlyList<Version> installedSdks = [sdkVersion];
        this._dotNetVersion.GetInstalledSdksAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(installedSdks);

        await this._bulkTemplateUpdater.BulkUpdateAsync(
            templateRepository: "git@github.com:template/repo.git",
            trackingFileName: string.Empty,
            packagesFileName: "/packages.json",
            workFolder: this._tempFolder,
            templateConfigFileName: "/template.json",
            releaseConfigFileName: "/release.json",
            repositories: [],
            cancellationToken: this.CancellationToken()
        );

        await this._dotNetVersion.Received(1).GetInstalledSdksAsync(cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public Task BulkUpdateWithSdkVersionNotInstalledThrows()
    {
        Version sdkVersion = new(9, 0, 300);

        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(
                new DotNetVersionSettings(
                    SdkVersion: sdkVersion.ToString(),
                    AllowPreRelease: false,
                    RollForward: "latestMajor"
                )
            );

        IReadOnlyList<Version> noSdks = [];
        this._dotNetVersion.GetInstalledSdksAsync(cancellationToken: Arg.Any<CancellationToken>()).Returns(noSdks);

        return Assert.ThrowsAsync<DotNetBuildErrorException>(() =>
            this
                ._bulkTemplateUpdater.BulkUpdateAsync(
                    templateRepository: "git@github.com:template/repo.git",
                    trackingFileName: string.Empty,
                    packagesFileName: "/packages.json",
                    workFolder: this._tempFolder,
                    templateConfigFileName: "/template.json",
                    releaseConfigFileName: "/release.json",
                    repositories: [],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task BulkUpdateWithCleanupFileExistingInTemplateFolderThrows()
    {
        string conflictingFile = Path.Combine(this._tempFolder, "old-file.txt");
        await File.WriteAllTextAsync(
            path: conflictingFile,
            contents: "test",
            cancellationToken: this.CancellationToken()
        );

        TemplateConfig configWithCleanup = new(
            general: new GeneralTemplateConfig(files: []),
            gitHub: new GitHubTemplateConfig(
                issueTemplates: false,
                pullRequestTemplates: false,
                actions: false,
                linters: false,
                files: [],
                dependabot: new DependabotTemplateConfig(generate: false),
                labels: new LabelsTemplateConfig(generate: false)
            ),
            dotNet: new DotnetTemplateConfig(globalJson: false, jetBrainsDotSettings: false, files: []),
            cleanup: new CleanupTemplateConfig(
                files: new Dictionary<string, string>(StringComparer.Ordinal) { ["old-file.txt"] = "chore" }
            )
        );

        this._templateConfigLoader.LoadConfigAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(configWithCleanup);

        await Assert.ThrowsAsync<InvalidTemplateConfigException>(() =>
            this
                ._bulkTemplateUpdater.BulkUpdateAsync(
                    templateRepository: "git@github.com:template/repo.git",
                    trackingFileName: string.Empty,
                    packagesFileName: "/packages.json",
                    workFolder: this._tempFolder,
                    templateConfigFileName: "/template.json",
                    releaseConfigFileName: "/release.json",
                    repositories: [],
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    private static TemplateConfig DependabotEnabledTemplateConfig()
    {
        return new TemplateConfig(
            general: new GeneralTemplateConfig(files: []),
            gitHub: new GitHubTemplateConfig(
                issueTemplates: false,
                pullRequestTemplates: false,
                actions: false,
                linters: false,
                files: [],
                dependabot: new DependabotTemplateConfig(generate: true),
                labels: new LabelsTemplateConfig(generate: false)
            ),
            dotNet: new DotnetTemplateConfig(globalJson: false, jetBrainsDotSettings: false, files: []),
            cleanup: new CleanupTemplateConfig(files: [])
        );
    }

    private static void MockRepositoryMetadata(IGitRepository repository, string repoDir)
    {
        repository.WorkingDirectory.Returns(repoDir);
        repository.ClonePath.Returns(REPO_URL);
        repository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        repository.HeadRev.Returns("abc123deadbeef");
    }

    private async Task<string> PrepareRepoWithChangeLogAsync()
    {
        string repoDir = Path.Combine(this._tempFolder, "repo");
        Directory.CreateDirectory(repoDir);
        Directory.CreateDirectory(Path.Combine(repoDir, ".github"));
        LibGit2Sharp.Repository.Init(repoDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(repoDir, "CHANGELOG.md"),
            contents: "# Changelog",
            cancellationToken: this.CancellationToken()
        );

        return repoDir;
    }

    private void MockDependabotUpdateContext(string repoDir, string dependabotContent)
    {
        this._templateConfigLoader.LoadConfigAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(DependabotEnabledTemplateConfig());

        this._dotNetFilesDetector.FindAsync(baseFolder: repoDir, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DotNetFiles(SourceDirectory: repoDir, Solutions: [], Projects: []));

        this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
                repoContext: Arg.Any<RepoContext>(),
                templateFolder: Arg.Any<string>(),
                dotNetFiles: Arg.Any<DotNetFiles>(),
                packages: Arg.Any<IReadOnlyList<PackageUpdate>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(dependabotContent);
    }

    [Fact]
    public async Task BulkUpdateWithMissingDependabotConfigWritesAndCommitsNewConfigAsync()
    {
        string repoDir = await this.PrepareRepoWithChangeLogAsync();
        const string dependabotContent = "updates: []\n";
        this.MockDependabotUpdateContext(repoDir: repoDir, dependabotContent: dependabotContent);

        IGitRepository repoRepository = GetSubstitute<IGitRepository>();
        MockRepositoryMetadata(repository: repoRepository, repoDir: repoDir);

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            repoRepository.Active.Returns(realRepo);

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: Arg.Any<string>(),
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(repoRepository);

            await this._bulkTemplateUpdater.BulkUpdateAsync(
                templateRepository: "git@github.com:template/repo.git",
                trackingFileName: string.Empty,
                packagesFileName: "/packages.json",
                workFolder: this._tempFolder,
                templateConfigFileName: "/template.json",
                releaseConfigFileName: "/release.json",
                repositories: [REPO_URL],
                cancellationToken: this.CancellationToken()
            );

            string dependabotConfigPath = Path.Combine(repoDir, ".github", "dependabot.yml");
            Assert.True(File.Exists(dependabotConfigPath), "Expected dependabot.yml to have been created");
            Assert.Equal(
                dependabotContent,
                await File.ReadAllTextAsync(path: dependabotConfigPath, cancellationToken: this.CancellationToken())
            );
            await repoRepository
                .Received(1)
                .CommitAsync(
                    message: "[Dependabot] Updated configuration",
                    cancellationToken: Arg.Any<CancellationToken>()
                );
            await repoRepository.Received(1).PushAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task BulkUpdateWithMatchingDependabotConfigDoesNotRewriteOrCommitAsync()
    {
        string repoDir = await this.PrepareRepoWithChangeLogAsync();
        const string dependabotContent = "updates: []\n";
        this.MockDependabotUpdateContext(repoDir: repoDir, dependabotContent: dependabotContent);

        string dependabotConfigPath = Path.Combine(repoDir, ".github", "dependabot.yml");
        await File.WriteAllTextAsync(
            path: dependabotConfigPath,
            contents: dependabotContent,
            cancellationToken: this.CancellationToken()
        );

        IGitRepository repoRepository = GetSubstitute<IGitRepository>();
        MockRepositoryMetadata(repository: repoRepository, repoDir: repoDir);

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            repoRepository.Active.Returns(realRepo);

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: Arg.Any<string>(),
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(repoRepository);

            await this._bulkTemplateUpdater.BulkUpdateAsync(
                templateRepository: "git@github.com:template/repo.git",
                trackingFileName: string.Empty,
                packagesFileName: "/packages.json",
                workFolder: this._tempFolder,
                templateConfigFileName: "/template.json",
                releaseConfigFileName: "/release.json",
                repositories: [REPO_URL],
                cancellationToken: this.CancellationToken()
            );

            await repoRepository
                .DidNotReceive()
                .CommitAsync(
                    message: "[Dependabot] Updated configuration",
                    cancellationToken: Arg.Any<CancellationToken>()
                );
            await repoRepository.DidNotReceive().PushAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task BulkUpdateWithDifferentDependabotConfigRewritesAndCommitsAsync()
    {
        string repoDir = await this.PrepareRepoWithChangeLogAsync();
        const string dependabotContent = "updates: []\n";
        this.MockDependabotUpdateContext(repoDir: repoDir, dependabotContent: dependabotContent);

        string dependabotConfigPath = Path.Combine(repoDir, ".github", "dependabot.yml");
        await File.WriteAllTextAsync(
            path: dependabotConfigPath,
            contents: "updates: [old]\n",
            cancellationToken: this.CancellationToken()
        );

        IGitRepository repoRepository = GetSubstitute<IGitRepository>();
        MockRepositoryMetadata(repository: repoRepository, repoDir: repoDir);

        using (LibGit2Sharp.Repository realRepo = new(repoDir))
        {
            repoRepository.Active.Returns(realRepo);

            this._gitRepositoryFactory.OpenOrCloneAsync(
                    workDir: Arg.Any<string>(),
                    repoUrl: REPO_URL,
                    cancellationToken: Arg.Any<CancellationToken>()
                )
                .Returns(repoRepository);

            await this._bulkTemplateUpdater.BulkUpdateAsync(
                templateRepository: "git@github.com:template/repo.git",
                trackingFileName: string.Empty,
                packagesFileName: "/packages.json",
                workFolder: this._tempFolder,
                templateConfigFileName: "/template.json",
                releaseConfigFileName: "/release.json",
                repositories: [REPO_URL],
                cancellationToken: this.CancellationToken()
            );

            Assert.Equal(
                dependabotContent,
                await File.ReadAllTextAsync(path: dependabotConfigPath, cancellationToken: this.CancellationToken())
            );
            await repoRepository
                .Received(1)
                .CommitAsync(
                    message: "[Dependabot] Updated configuration",
                    cancellationToken: Arg.Any<CancellationToken>()
                );
            await repoRepository.Received(1).PushAsync(Arg.Any<CancellationToken>());
        }
    }
}
