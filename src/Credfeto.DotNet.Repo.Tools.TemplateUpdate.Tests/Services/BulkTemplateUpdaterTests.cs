using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class BulkTemplateUpdaterTests : TestBase, IDisposable
{
    private readonly string _tempFolder;
    private readonly IBulkTemplateUpdater _bulkTemplateUpdater;

    private readonly ITrackingCache _trackingCache;
    private readonly IBulkPackageConfigLoader _bulkPackageConfigLoader;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly ITemplateConfigLoader _templateConfigLoader;
    private readonly IGlobalJson _globalJson;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IReleaseConfigLoader _releaseConfigLoader;

    public BulkTemplateUpdaterTests()
    {
        this._tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._tempFolder);

        this._trackingCache = GetSubstitute<ITrackingCache>();
        this._bulkPackageConfigLoader = GetSubstitute<IBulkPackageConfigLoader>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        this._templateConfigLoader = GetSubstitute<ITemplateConfigLoader>();
        this._globalJson = GetSubstitute<IGlobalJson>();
        this._dotNetVersion = GetSubstitute<IDotNetVersion>();
        this._releaseConfigLoader = GetSubstitute<IReleaseConfigLoader>();

        this._bulkTemplateUpdater = new BulkTemplateUpdater(
            trackingCache: this._trackingCache,
            globalJson: this._globalJson,
            dotNetFilesDetector: GetSubstitute<IDotNetFilesDetector>(),
            dotNetVersion: this._dotNetVersion,
            dotNetSolutionCheck: GetSubstitute<IDotNetSolutionCheck>(),
            dotNetBuild: GetSubstitute<IDotNetBuild>(),
            releaseConfigLoader: this._releaseConfigLoader,
            releaseGeneration: GetSubstitute<IReleaseGeneration>(),
            gitRepositoryFactory: this._gitRepositoryFactory,
            bulkPackageConfigLoader: this._bulkPackageConfigLoader,
            fileUpdater: GetSubstitute<IFileUpdater>(),
            dependaBotConfigBuilder: GetSubstitute<IDependaBotConfigBuilder>(),
            labelsBuilder: GetSubstitute<ILabelsBuilder>(),
            templateConfigLoader: this._templateConfigLoader,
            logger: GetSubstitute<ILogger<BulkTemplateUpdater>>()
        );

        this.SetupDefaultMocks();
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempFolder))
        {
            Directory.Delete(path: this._tempFolder, recursive: true);
        }
    }

    private void SetupDefaultMocks()
    {
        IGitRepository templateRepo = GetSubstitute<IGitRepository>();
        templateRepo.WorkingDirectory.Returns(this._tempFolder);

        this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: Arg.Any<string>(),
                repoUrl: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(templateRepo);

        this._bulkPackageConfigLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
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

        this._releaseConfigLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
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
    public async Task BulkUpdateWithNonexistentTrackingFileDoesNotLoadTracking()
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
        this._globalJson.LoadGlobalJsonAsync(
                baseFolder: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new DotNetVersionSettings(SdkVersion: null, AllowPreRelease: false, RollForward: "latestMajor"));

        IReadOnlyList<Version> installedSdks = [];
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
}
