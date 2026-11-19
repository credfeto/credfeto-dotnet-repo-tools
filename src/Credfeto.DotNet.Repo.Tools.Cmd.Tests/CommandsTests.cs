using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
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
    private readonly IGitRepository _gitRepository;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGitRepositoryListLoader _gitRepositoryListLoader;

    public CommandsTests(ITestOutputHelper output)
        : base(output)
    {
        this._gitRepositoryListLoader = GetSubstitute<IGitRepositoryListLoader>();
        this._bulkCodeCleanUp = GetSubstitute<IBulkCodeCleanUp>();
        this._bulkPackageUpdater = GetSubstitute<IBulkPackageUpdater>();
        this._bulkTemplateUpdater = GetSubstitute<IBulkTemplateUpdater>();
        this._bulkDependencyReducer = GetSubstitute<IBulkDependencyReducer>();
        this._dependencyReducer = GetSubstitute<IDependencyReducer>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();
        this._gitRepository = GetSubstitute<IGitRepository>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        this._gitRepositoryFactory.OpenExisting(Arg.Any<string>()).Returns(this._gitRepository);
        this._commands = new Commands(
            gitRepositoryListLoader: this._gitRepositoryListLoader,
            bulkCodeCleanUp: this._bulkCodeCleanUp,
            bulkPackageUpdater: this._bulkPackageUpdater,
            bulkTemplateUpdater: this._bulkTemplateUpdater,
            bulkDependencyReducer: this._bulkDependencyReducer,
            dependencyReducer: this._dependencyReducer,
            dotNetFilesDetector: this._dotNetFilesDetector,
            gitRepositoryFactory: this._gitRepositoryFactory,
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

    private ValueTask ReceivedBulkPackageUpdateAsync(int times)
    {
        return this
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
    }

    private ValueTask ReceivedBulkTemplateUpdateAsync(int times)
    {
        return this
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
    }

    private ValueTask ReceivedBulkCodeCleanUpAsync(int times)
    {
        return this
            ._bulkCodeCleanUp.ReceivedWithAnyArgs(times)
            .BulkUpdateAsync(string.Empty, string.Empty, string.Empty, [], default);
    }

    private ValueTask ReceivedBulkDependencyReduceAsync(int times)
    {
        return this
            ._bulkDependencyReducer.ReceivedWithAnyArgs(times)
            .BulkUpdateAsync(string.Empty, string.Empty, string.Empty, [], default);
    }

    private ValueTask<DotNetFiles> ReceivedFindAsync(int times)
    {
        return this._dotNetFilesDetector.ReceivedWithAnyArgs(times).FindAsync(string.Empty, default);
    }

    private ValueTask ReceivedBulkPackageUpdateWithSingleRepoAsync(string expectedRepo)
    {
        return this
            ._bulkPackageUpdater.Received(1)
            .BulkUpdateAsync(
                templateRepository: Arg.Any<string>(),
                cacheFileName: Arg.Any<string?>(),
                trackingFileName: Arg.Any<string>(),
                packagesFileName: Arg.Any<string>(),
                workFolder: Arg.Any<string>(),
                releaseConfigFileName: Arg.Any<string>(),
                additionalNugetSources: Arg.Any<IReadOnlyList<string>>(),
                repositories: Arg.Is<IReadOnlyList<string>>(r => r is object && r.Count == 1 && r[0] == expectedRepo),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdatePackagesWithNullSourceCallsBulkPackageUpdaterAsync()
    {
        this.SetupOneRepository();

        await this._commands.UpdatePackagesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cache: null,
            source: null,
            cancellationToken: this.CancellationToken()
        );

        await this.ReceivedBulkPackageUpdateAsync(1);
    }

    [Fact]
    public async Task UpdatePackagesWithSourceCallsBulkPackageUpdaterAsync()
    {
        this.SetupOneRepository();

        await this._commands.UpdatePackagesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cache: null,
            source: ["https://nuget.org/v3/index.json"],
            cancellationToken: this.CancellationToken()
        );

        await this.ReceivedBulkPackageUpdateAsync(1);
    }

    [Fact]
    public async Task UpdatePackagesPropagatesCancellationTokenAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        this.SetupOneRepository();

        await this._commands.UpdatePackagesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cache: null,
            source: null,
            cancellationToken: cancellationToken
        );

        await this
            ._gitRepositoryListLoader.Received(1)
            .LoadAsync(path: Arg.Any<string>(), cancellationToken: cancellationToken);
        await this
            ._bulkPackageUpdater.Received(1)
            .BulkUpdateAsync(
                templateRepository: Arg.Any<string>(),
                cacheFileName: Arg.Any<string?>(),
                trackingFileName: Arg.Any<string>(),
                packagesFileName: Arg.Any<string>(),
                workFolder: Arg.Any<string>(),
                releaseConfigFileName: Arg.Any<string>(),
                additionalNugetSources: Arg.Any<IReadOnlyList<string>>(),
                repositories: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: cancellationToken
            );
    }

    [Fact]
    public async Task UpdatePackagesStopsAtFirstAwaitWhenCancellationTokenAlreadyCancelledAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        this._gitRepositoryListLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: cancellationToken)
            .Returns<IReadOnlyList<string>>(_ => throw new OperationCanceledException(cancellationToken));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            this._commands.UpdatePackagesAsync(
                repositories: "repos.lst",
                template: "https://template.git",
                tracking: "tracking.json",
                packages: "packages.json",
                work: "/work",
                release: "release.config",
                cache: null,
                source: null,
                cancellationToken: cancellationToken
            )
        );

        await this.ReceivedBulkPackageUpdateAsync(0);
    }

    [Fact]
    public async Task UpdatePackagesReturnsErrorExitCodeWhenNoRepositoriesFoundAsync()
    {
        this.SetupRepositories([]);

        int exitCode = await this._commands.UpdatePackagesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cache: null,
            source: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task UpdatePackagesExcludesTemplateRepositoryFromRepositoryListAsync()
    {
        this.SetupRepositories(["https://template.git", "https://repo1.git"]);

        await this._commands.UpdatePackagesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cache: null,
            source: null,
            cancellationToken: this.CancellationToken()
        );

        await this.ReceivedBulkPackageUpdateWithSingleRepoAsync("https://repo1.git");
    }

    [Fact]
    public async Task UpdateFromTemplateCallsBulkTemplateUpdaterAsync()
    {
        this.SetupOneRepository();

        await this._commands.UpdateFromTemplateAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            templateConfig: "template.config",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cancellationToken: this.CancellationToken()
        );

        await this.ReceivedBulkTemplateUpdateAsync(1);
    }

    [Fact]
    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute mock setup requires calling async methods without awaiting"
    )]
    public async Task UpdateFromTemplateReturnsErrorExitCodeInsteadOfThrowingWhenBulkTemplateUpdaterFailsAsync()
    {
        this.SetupOneRepository();
        this._bulkTemplateUpdater.BulkUpdateAsync(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                this.CancellationToken()
            )
            .ReturnsForAnyArgs(_ =>
                ValueTask.FromException(new InvalidOperationException("Simulated unexpected failure"))
            );

        int exitCode = await this._commands.UpdateFromTemplateAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            templateConfig: "template.config",
            tracking: "tracking.json",
            packages: "packages.json",
            work: "/work",
            release: "release.config",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task CodeCleanupCallsBulkCodeCleanUpAsync()
    {
        this.SetupOneRepository();

        await this._commands.CodeCleanupAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            work: "/work",
            cancellationToken: this.CancellationToken()
        );

        await this.ReceivedBulkCodeCleanUpAsync(1);
    }

    [Fact]
    public async Task ReduceDependenciesCallsBulkDependencyReducerAsync()
    {
        this.SetupOneRepository();

        await this._commands.ReduceDependenciesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            work: "/work",
            cancellationToken: this.CancellationToken()
        );

        await this.ReceivedBulkDependencyReduceAsync(1);
    }

    [Fact]
    public async Task ReduceDependenciesPropagatesCancellationTokenAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        this.SetupOneRepository();

        await this._commands.ReduceDependenciesAsync(
            repositories: "repos.lst",
            template: "https://template.git",
            tracking: "tracking.json",
            work: "/work",
            cancellationToken: cancellationToken
        );

        await this
            ._gitRepositoryListLoader.Received(1)
            .LoadAsync(path: Arg.Any<string>(), cancellationToken: cancellationToken);
        await this
            ._bulkDependencyReducer.Received(1)
            .BulkUpdateAsync(
                templateRepository: Arg.Any<string>(),
                trackingFileName: Arg.Any<string>(),
                workFolder: Arg.Any<string>(),
                repositories: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: cancellationToken
            );
    }

    [Fact]
    public async Task ReduceDependenciesStopsAtFirstAwaitWhenCancellationTokenAlreadyCancelledAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        this._gitRepositoryListLoader.LoadAsync(path: Arg.Any<string>(), cancellationToken: cancellationToken)
            .Returns<IReadOnlyList<string>>(_ => throw new OperationCanceledException(cancellationToken));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            this._commands.ReduceDependenciesAsync(
                repositories: "repos.lst",
                template: "https://template.git",
                tracking: "tracking.json",
                work: "/work",
                cancellationToken: cancellationToken
            )
        );

        await this.ReceivedBulkDependencyReduceAsync(0);
    }

    [Fact]
    public async Task CheckDependenciesCallsDotNetFilesDetectorAsync()
    {
        await this._commands.CheckDependenciesAsync(sourceFolder: "/work", cancellationToken: this.CancellationToken());

        await this.ReceivedFindAsync(1);
    }

    [Fact]
    public async Task CheckDependenciesPropagatesCancellationTokenAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;

        await this._commands.CheckDependenciesAsync(sourceFolder: "/work", cancellationToken: cancellationToken);

        await this
            ._dotNetFilesDetector.Received(1)
            .FindAsync(baseFolder: Arg.Any<string>(), cancellationToken: cancellationToken);
        await this
            ._dependencyReducer.Received(1)
            .CheckReferencesAsync(
                dotNetFiles: Arg.Any<DotNetFiles>(),
                config: Arg.Any<ReferenceConfig>(),
                cancellationToken: cancellationToken
            );
    }

    [Fact]
    public async Task CheckDependenciesStopsAtFirstAwaitWhenCancellationTokenAlreadyCancelledAsync()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        this._dotNetFilesDetector.FindAsync(baseFolder: Arg.Any<string>(), cancellationToken: cancellationToken)
            .Returns<DotNetFiles>(_ => throw new OperationCanceledException(cancellationToken));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            this._commands.CheckDependenciesAsync(sourceFolder: "/work", cancellationToken: cancellationToken)
        );

        await this
            ._dependencyReducer.DidNotReceiveWithAnyArgs()
            .CheckReferencesAsync(
                dotNetFiles: Arg.Any<DotNetFiles>(),
                config: Arg.Any<ReferenceConfig>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "NSubstitute call verification does not create a repository"
    )]
    public async Task CheckDependenciesOpensExistingRepositoryForSourceFolderAsync()
    {
        await this._commands.CheckDependenciesAsync(sourceFolder: "/work", cancellationToken: this.CancellationToken());

        this._gitRepositoryFactory.Received(1).OpenExisting("/work");
        this._gitRepository.Received(1).Dispose();
    }

    [Fact]
    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute mock setup requires calling async methods without awaiting"
    )]
    public async Task CheckDependenciesCommitsEachSuccessfulRemovalAsync()
    {
        string firstProject = Path.Combine(path1: this.TempFolder, path2: "First.csproj");
        string secondProject = Path.Combine(path1: this.TempFolder, path2: "Second.csproj");
        CancellationToken cancellationToken = this.CancellationToken();

        this._dependencyReducer.CheckReferencesAsync(
                dotNetFiles: Arg.Any<DotNetFiles>(),
                config: Arg.Any<ReferenceConfig>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(call =>
                RemoveTwoReferencesAsync(
                    config: call.ArgAt<ReferenceConfig>(1),
                    firstProject: firstProject,
                    secondProject: secondProject,
                    cancellationToken: cancellationToken
                )
            );

        int exitCode = await this._commands.CheckDependenciesAsync(
            sourceFolder: "/work",
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: 0, actual: exitCode);
        await this
            ._gitRepository.Received(1)
            .CommitNamedAsync(
                message: "Removed reference to A",
                files: Arg.Is<IReadOnlyList<string>>(f => IsSingleFile(f, firstProject)),
                cancellationToken: cancellationToken
            );
        await this
            ._gitRepository.Received(1)
            .CommitNamedAsync(
                message: "Removed reference to B",
                files: Arg.Is<IReadOnlyList<string>>(f => IsSingleFile(f, secondProject)),
                cancellationToken: cancellationToken
            );
        await this._gitRepository.DidNotReceiveWithAnyArgs().PushAsync(cancellationToken);
        await this
            ._gitRepository.DidNotReceiveWithAnyArgs()
            .ResetToDefaultBranchAsync(upstream: string.Empty, cancellationToken: cancellationToken);
    }

    private static bool IsSingleFile(IReadOnlyList<string> files, string expected)
    {
        return files.Count == 1 && StringComparer.Ordinal.Equals(files[0], expected);
    }

    private static async ValueTask<bool> RemoveTwoReferencesAsync(
        ReferenceConfig config,
        string firstProject,
        string secondProject,
        CancellationToken cancellationToken
    )
    {
        await config.OnSuccessfulRemoval(arg1: firstProject, arg2: "Removed reference to A", arg3: cancellationToken);
        await config.OnSuccessfulRemoval(arg1: secondProject, arg2: "Removed reference to B", arg3: cancellationToken);

        return true;
    }
}
