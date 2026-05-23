using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Tests;

public sealed class CommandsTests : LoggingFolderCleanupTestBase
{
    private readonly IBulkCodeCleanUp _bulkCodeCleanUp;
    private readonly IBulkDependencyReducer _bulkDependencyReducer;
    private readonly IBulkPackageUpdater _bulkPackageUpdater;
    private readonly IBulkTemplateUpdater _bulkTemplateUpdater;
    private readonly Commands _commands;
    private readonly IDependencyReducer _dependencyReducer;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IGitRepositoryListLoader _gitRepositoryListLoader;

    public CommandsTests(ITestOutputHelper output)
        : base(output)
    {
        this._gitRepositoryListLoader = Substitute.For<IGitRepositoryListLoader>();
        this._bulkCodeCleanUp = Substitute.For<IBulkCodeCleanUp>();
        this._bulkPackageUpdater = Substitute.For<IBulkPackageUpdater>();
        this._bulkTemplateUpdater = Substitute.For<IBulkTemplateUpdater>();
        this._bulkDependencyReducer = Substitute.For<IBulkDependencyReducer>();
        this._dependencyReducer = Substitute.For<IDependencyReducer>();
        this._dotNetFilesDetector = Substitute.For<IDotNetFilesDetector>();
        this._commands = new Commands(
            gitRepositoryListLoader: this._gitRepositoryListLoader,
            bulkCodeCleanUp: this._bulkCodeCleanUp,
            bulkPackageUpdater: this._bulkPackageUpdater,
            bulkTemplateUpdater: this._bulkTemplateUpdater,
            bulkDependencyReducer: this._bulkDependencyReducer,
            dependencyReducer: this._dependencyReducer,
            dotNetFilesDetector: this._dotNetFilesDetector,
            logger: this.GetTypedLogger<Commands>()
        );
    }

    private void SetupRepositories(IReadOnlyList<string> repos)
    {
        this._gitRepositoryListLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(repos);
    }

    private void SetupOneRepository()
    {
        this.SetupRepositories(["https://repo1.git"]);
    }

    private ValueTask ReceivedBulkPackageUpdateAsync(int times) =>
        this
            ._bulkPackageUpdater.ReceivedWithAnyArgs(times)
            .BulkUpdateAsync(
                string.Empty,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                [],
                default
            );

    private ValueTask ReceivedBulkTemplateUpdateAsync(int times) =>
        this
            ._bulkTemplateUpdater.ReceivedWithAnyArgs(times)
            .BulkUpdateAsync(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                default
            );

    private ValueTask ReceivedBulkCodeCleanUpAsync(int times) =>
        this
            ._bulkCodeCleanUp.ReceivedWithAnyArgs(times)
            .BulkUpdateAsync(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, [], default);

    private ValueTask ReceivedBulkDependencyReduceAsync(int times) =>
        this
            ._bulkDependencyReducer.ReceivedWithAnyArgs(times)
            .BulkUpdateAsync(string.Empty, string.Empty, string.Empty, [], default);

    private ValueTask<DotNetFiles> ReceivedFindAsync(int times) =>
        this._dotNetFilesDetector.ReceivedWithAnyArgs(times).FindAsync(string.Empty, default);

    private ValueTask ReceivedBulkPackageUpdateWithSingleRepoAsync(string expectedRepo) =>
        this
            ._bulkPackageUpdater.Received(1)
            .BulkUpdateAsync(
                templateRepository: Arg.Any<string>(),
                cacheFileName: Arg.Any<string?>(),
                trackingFileName: Arg.Any<string>(),
                packagesFileName: Arg.Any<string>(),
                workFolder: Arg.Any<string>(),
                releaseConfigFileName: Arg.Any<string>(),
                additionalNugetSources: Arg.Any<IReadOnlyList<string>>(),
                repositories: Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == expectedRepo),
                cancellationToken: Arg.Any<CancellationToken>()
            );

    [Fact]
    public async Task UpdatePackagesWithNullSourceCallsBulkPackageUpdaterAsync()
    {
        this.SetupOneRepository();

        await this._commands.UpdatePackagesAsync(
            repositoriesFileName: "repos.lst",
            templateRepository: "https://template.git",
            cacheFileName: null,
            trackingFileName: "tracking.json",
            packagesFileName: "packages.json",
            workFolder: "/work",
            releaseConfigFileName: "release.config",
            source: null
        );

        await this.ReceivedBulkPackageUpdateAsync(1);
    }

    [Fact]
    public async Task UpdatePackagesWithSourceCallsBulkPackageUpdaterAsync()
    {
        this.SetupOneRepository();

        await this._commands.UpdatePackagesAsync(
            repositoriesFileName: "repos.lst",
            templateRepository: "https://template.git",
            cacheFileName: null,
            trackingFileName: "tracking.json",
            packagesFileName: "packages.json",
            workFolder: "/work",
            releaseConfigFileName: "release.config",
            source: ["https://nuget.org/v3/index.json"]
        );

        await this.ReceivedBulkPackageUpdateAsync(1);
    }

    [Fact]
    public Task UpdatePackagesThrowsWhenNoRepositoriesFoundAsync()
    {
        this.SetupRepositories([]);

        return Assert.ThrowsAsync<InvalidOperationException>(() =>
            this._commands.UpdatePackagesAsync(
                repositoriesFileName: "repos.lst",
                templateRepository: "https://template.git",
                cacheFileName: null,
                trackingFileName: "tracking.json",
                packagesFileName: "packages.json",
                workFolder: "/work",
                releaseConfigFileName: "release.config",
                source: null
            )
        );
    }

    [Fact]
    public async Task UpdatePackagesExcludesTemplateRepositoryFromRepositoryListAsync()
    {
        this.SetupRepositories(["https://template.git", "https://repo1.git"]);

        await this._commands.UpdatePackagesAsync(
            repositoriesFileName: "repos.lst",
            templateRepository: "https://template.git",
            cacheFileName: null,
            trackingFileName: "tracking.json",
            packagesFileName: "packages.json",
            workFolder: "/work",
            releaseConfigFileName: "release.config",
            source: null
        );

        await this.ReceivedBulkPackageUpdateWithSingleRepoAsync("https://repo1.git");
    }

    [Fact]
    public async Task UpdateFromTemplateCallsBulkTemplateUpdaterAsync()
    {
        this.SetupOneRepository();

        await this._commands.UpdateFromTemplateAsync(
            repositoriesFileName: "repos.lst",
            templateRepository: "https://template.git",
            templateConfigFileName: "template.config",
            trackingFileName: "tracking.json",
            packagesFileName: "packages.json",
            workFolder: "/work",
            releaseConfigFileName: "release.config"
        );

        await this.ReceivedBulkTemplateUpdateAsync(1);
    }

    [Fact]
    public async Task CodeCleanupCallsBulkCodeCleanUpAsync()
    {
        this.SetupOneRepository();

        await this._commands.CodeCleanupAsync(
            repositoriesFileName: "repos.lst",
            templateRepository: "https://template.git",
            trackingFileName: "tracking.json",
            packagesFileName: "packages.json",
            workFolder: "/work",
            releaseConfigFileName: "release.config"
        );

        await this.ReceivedBulkCodeCleanUpAsync(1);
    }

    [Fact]
    public async Task ReduceDependenciesCallsBulkDependencyReducerAsync()
    {
        this.SetupOneRepository();

        await this._commands.ReduceDependenciesAsync(
            repositoriesFileName: "repos.lst",
            templateRepository: "https://template.git",
            trackingFileName: "tracking.json",
            workFolder: "/work"
        );

        await this.ReceivedBulkDependencyReduceAsync(1);
    }

    [Fact]
    public async Task CheckDependenciesCallsDotNetFilesDetectorAsync()
    {
        await this._commands.CheckDependenciesAsync(workFolder: "/work");

        await this.ReceivedFindAsync(1);
    }
}
