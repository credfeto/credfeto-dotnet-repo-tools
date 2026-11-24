using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Cmd.Setup;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Tests;

// Runs the real command line (parsing, routing, help and exit codes) against substituted services.
// ConsoleAppFramework keeps its state in statics, so every command-line test lives in this one class:
// xunit runs the tests of a class one at a time.
public sealed class CommandLineTests : TestBase
{
    private const int SUCCESS = 0;
    private const int ERROR = 1;
    private const int HELP_SHOWN = 129;

    private static readonly char[] ArgumentSeparator = [' '];

    private static readonly string[] ExpectedSources = ["https://a/index.json", "https://b/index.json"];

    private readonly IBulkCodeCleanUp _bulkCodeCleanUp;
    private readonly IBulkDependencyReducer _bulkDependencyReducer;
    private readonly IBulkPackageUpdater _bulkPackageUpdater;
    private readonly IBulkTemplateUpdater _bulkTemplateUpdater;
    private readonly IDependencyReducer _dependencyReducer;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGitRepositoryListLoader _gitRepositoryListLoader;

    public CommandLineTests()
    {
        this._bulkCodeCleanUp = GetSubstitute<IBulkCodeCleanUp>();
        this._bulkDependencyReducer = GetSubstitute<IBulkDependencyReducer>();
        this._bulkPackageUpdater = GetSubstitute<IBulkPackageUpdater>();
        this._bulkTemplateUpdater = GetSubstitute<IBulkTemplateUpdater>();
        this._dependencyReducer = GetSubstitute<IDependencyReducer>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();
        this._gitRepositoryFactory = GetSubstitute<IGitRepositoryFactory>();
        this._gitRepositoryListLoader = GetSubstitute<IGitRepositoryListLoader>();

        this._gitRepositoryListLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(["https://template.git", "https://repo1.git"]);
        this._gitRepositoryFactory.OpenExisting(Arg.Any<string>()).Returns(GetSubstitute<IGitRepository>());
    }

    [Theory]
    [InlineData(
        "update-packages -r repos.lst -m https://template.git -c cache.json -t tracking -p packages.json -w work -l release.config -s https://a/index.json -s https://b/index.json"
    )]
    [InlineData(
        "update-packages --repositories repos.lst --template https://template.git --cache cache.json --tracking tracking --packages packages.json --work work --release release.config --source https://a/index.json,https://b/index.json"
    )]
    public async Task UpdatePackagesBindsEveryOptionAsync(string commandLine)
    {
        CommandLineResult result = await this.RunAsync(commandLine);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._bulkPackageUpdater.Received(1)
            .BulkUpdateAsync(
                templateRepository: "https://template.git",
                cacheFileName: "cache.json",
                trackingFileName: "tracking",
                packagesFileName: "packages.json",
                workFolder: "work",
                releaseConfigFileName: "release.config",
                additionalNugetSources: Arg.Is<IReadOnlyList<string>>(sources =>
                    sources.SequenceEqual(ExpectedSources, StringComparer.Ordinal)
                ),
                repositories: Arg.Is<IReadOnlyList<string>>(repositories => IsOnlyRepo1(repositories)),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdatePackagesWithoutOptionalOptionsPassesNoCacheAndNoSourcesAsync()
    {
        CommandLineResult result = await this.RunAsync(
            "update-packages -r repos.lst -m https://template.git -t tracking -p packages.json -w work -l release.config"
        );

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._bulkPackageUpdater.Received(1)
            .BulkUpdateAsync(
                templateRepository: "https://template.git",
                cacheFileName: null,
                trackingFileName: "tracking",
                packagesFileName: "packages.json",
                workFolder: "work",
                releaseConfigFileName: "release.config",
                additionalNugetSources: Arg.Is<IReadOnlyList<string>>(sources => sources.Count == 0),
                repositories: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateTemplateBindsEveryOptionAsync()
    {
        CommandLineResult result = await this.RunAsync(
            "update-template -r repos.lst -m https://template.git -c template.config -t tracking -p packages.json -w work -l release.config"
        );

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._bulkTemplateUpdater.Received(1)
            .BulkUpdateAsync(
                templateRepository: "https://template.git",
                trackingFileName: "tracking",
                packagesFileName: "packages.json",
                workFolder: "work",
                templateConfigFileName: "template.config",
                releaseConfigFileName: "release.config",
                repositories: Arg.Is<IReadOnlyList<string>>(repositories => IsOnlyRepo1(repositories)),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task CodeCleanupBindsEveryOptionAsync()
    {
        CommandLineResult result = await this.RunAsync(
            "code-cleanup --repositories repos.lst --template https://template.git --tracking tracking --work work"
        );

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._bulkCodeCleanUp.Received(1)
            .BulkUpdateAsync(
                templateRepository: "https://template.git",
                trackingFileName: "tracking",
                workFolder: "work",
                repositories: Arg.Is<IReadOnlyList<string>>(repositories => IsOnlyRepo1(repositories)),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ReduceDependenciesBindsEveryOptionAsync()
    {
        CommandLineResult result = await this.RunAsync(
            "reduce-dependencies -r repos.lst -m https://template.git -t tracking -w work"
        );

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._bulkDependencyReducer.Received(1)
            .BulkUpdateAsync(
                templateRepository: "https://template.git",
                trackingFileName: "tracking",
                workFolder: "work",
                repositories: Arg.Is<IReadOnlyList<string>>(repositories => IsOnlyRepo1(repositories)),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "NSubstitute call verification does not create a repository"
    )]
    [InlineData("check-dependencies -s source")]
    [InlineData("check-dependencies --source-folder source")]
    public async Task CheckDependenciesBindsSourceFolderAsync(string commandLine)
    {
        CommandLineResult result = await this.RunAsync(commandLine);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._dotNetFilesDetector.Received(1)
            .FindAsync(baseFolder: "source", cancellationToken: Arg.Any<CancellationToken>());
        this._gitRepositoryFactory.Received(1).OpenExisting("source");
    }

    [Fact]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "NSubstitute call verification does not create a repository"
    )]
    public async Task ArgumentsAfterEndOfOptionsAreNotTreatedAsOptionsAsync()
    {
        CommandLineResult result = await this.RunAsync("check-dependencies -s source -- --help");

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        await this
            ._dotNetFilesDetector.Received(1)
            .FindAsync(baseFolder: "source", cancellationToken: Arg.Any<CancellationToken>());
        this._gitRepositoryFactory.Received(1).OpenExisting("source");
    }

    [Fact]
    public async Task ProgramShowsHelpAsync()
    {
        try
        {
            int exitCode = await Program.Main(["--help"]);

            Assert.Equal(expected: HELP_SHOWN, actual: exitCode);
        }
        finally
        {
            Environment.ExitCode = SUCCESS;
        }
    }

    [Fact]
    public async Task ProgramRunsCommandWithRealServicesAsync()
    {
        string missingFolder = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "updaterepo-missing-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            int exitCode = await Program.Main(["check-dependencies", "--source-folder", missingFolder]);

            Assert.Equal(expected: ERROR, actual: exitCode);
        }
        finally
        {
            Environment.ExitCode = SUCCESS;
        }
    }

    [Fact]
    public async Task ProgramSelfTestWithRealServicesSucceedsAsync()
    {
        try
        {
            int exitCode = await Program.Main(["--self-test"]);

            Assert.Equal(expected: SUCCESS, actual: exitCode);
        }
        finally
        {
            Environment.ExitCode = SUCCESS;
        }
    }

    [Fact]
    public async Task SelfTestSucceedsWithoutRunningAnythingAsync()
    {
        CommandLineResult result = await this.RunAsync("--self-test");

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        Assert.Contains(expectedSubstring: "Self test passed", actualString: result.Output, StringComparison.Ordinal);
        Assert.Empty(result.Error);
        await this.ReceivedNoBulkPackageUpdateAsync();
        await this
            ._gitRepositoryListLoader.DidNotReceiveWithAnyArgs()
            .LoadAsync(path: string.Empty, cancellationToken: this.CancellationToken());
    }

    [Fact]
    public async Task SelfTestWithMissingServiceExitsWithErrorAsync()
    {
        await using (StringWriter output = new())
        await using (StringWriter error = new())
        {
            int exitCode = await CommandLineRunner.RunAsync(
                args: ["--self-test"],
                configureServices: static _ => { },
                output: output,
                error: error,
                cancellationToken: this.CancellationToken()
            );

            Assert.Equal(expected: ERROR, actual: exitCode);
            Assert.StartsWith(
                expectedStartString: "Self test failed: ",
                actualString: error.ToString(),
                StringComparison.Ordinal
            );
        }
    }

    [Fact]
    public async Task CommandFailureExitsWithErrorAsync()
    {
        this._gitRepositoryListLoader.LoadAsync(
                path: Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns([]);

        CommandLineResult result = await this.RunAsync("code-cleanup -r repos.lst -m https://template.git -t t -w w");

        Assert.Equal(expected: ERROR, actual: result.ExitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task HelpListsEveryCommandAsync(string commandLine)
    {
        CommandLineResult result = await this.RunAsync(commandLine);

        Assert.Equal(expected: HELP_SHOWN, actual: result.ExitCode);
        AssertHelpIs(
            actual: result.Output,
            "Usage: Credfeto.DotNet.Repo.Tools.Cmd [command]",
            "",
            "Credfeto.DotNet.Repo.Tools.Cmd",
            "",
            "Commands:",
            "  update-packages        Update all packages in all repositories",
            "  update-template        Update repos from template in all repositories",
            "  code-cleanup           Perform code cleanup in all repositories",
            "  reduce-dependencies    Reduce dependencies in all repositories",
            "  check-dependencies     Reduce dependencies one folder",
            "",
            "Options:",
            "  -h, --help     Show help message",
            "  --version      Show version",
            "  --self-test    Check the application's services can be created"
        );
    }

    [Fact]
    public async Task NoArgumentsShowsHelpAndSucceedsAsync()
    {
        CommandLineResult result = await this.RunAsync(string.Empty);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        Assert.Contains(expectedSubstring: "update-packages", actualString: result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandHelpListsEveryOptionAsync()
    {
        CommandLineResult result = await this.RunAsync("update-packages --help");

        Assert.Equal(expected: HELP_SHOWN, actual: result.ExitCode);
        AssertHelpIs(
            actual: result.Output,
            "Usage: Credfeto.DotNet.Repo.Tools.Cmd update-packages [--repositories <String>] [--template <String>] [--cache <String>] [--tracking <String>] [--packages <String>] [--work <String>] [--release <String>] [--source <String>...] [--help]",
            "",
            "Update all packages in all repositories",
            "",
            "Options:",
            "  -r, --repositories <String>    repos.lst file containing list of repositories (Required)",
            "  -m, --template <String>        Template repository to clone (Required)",
            "  -c, --cache <String>           package cache file",
            "  -t, --tracking <String>        folder where to write tracking.json file (Required)",
            "  -p, --packages <String>        Packages.json file to load (Required)",
            "  -w, --work <String>            folder where to clone repositories (Required)",
            "  -l, --release <String>         release.config file to load (Required)",
            "  -s, --source <String>...       Urls to additional NuGet feeds to load",
            "  -h, --help                     Show help message"
        );
        await this.ReceivedNoBulkPackageUpdateAsync();
    }

    [Fact]
    public async Task VersionShowsProductAndVersionAsync()
    {
        CommandLineResult result = await this.RunAsync("--version");

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        Assert.StartsWith(
            expectedStartString: "Repository Update Tool ",
            actualString: result.Output,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("no-such-command")]
    [InlineData("--bogus")]
    [InlineData("--self-test extra")]
    [InlineData("update-packages")]
    [InlineData("update-packages -r repos.lst")]
    [InlineData("check-dependencies -s")]
    [InlineData("check-dependencies -s source --bogus 1")]
    public async Task BadArgumentsExitWithErrorWithoutRunningAnythingAsync(string commandLine)
    {
        CommandLineResult result = await this.RunAsync(commandLine);

        Assert.Equal(expected: ERROR, actual: result.ExitCode);
        Assert.NotEmpty(result.Error);
        await this.ReceivedNoBulkPackageUpdateAsync();
        await this
            ._dotNetFilesDetector.DidNotReceiveWithAnyArgs()
            .FindAsync(baseFolder: string.Empty, cancellationToken: this.CancellationToken());
    }

    private ValueTask ReceivedNoBulkPackageUpdateAsync()
    {
        return this
            ._bulkPackageUpdater.DidNotReceiveWithAnyArgs()
            .BulkUpdateAsync(
                templateRepository: string.Empty,
                cacheFileName: null,
                trackingFileName: string.Empty,
                packagesFileName: string.Empty,
                workFolder: string.Empty,
                releaseConfigFileName: string.Empty,
                additionalNugetSources: [],
                repositories: [],
                cancellationToken: default
            );
    }

    private static bool IsOnlyRepo1(IReadOnlyList<string> repositories)
    {
        return repositories is [{ } single] && StringComparer.Ordinal.Equals(x: single, y: "https://repo1.git");
    }

    private static void AssertHelpIs(string actual, params IReadOnlyList<string> expectedLines)
    {
        Assert.Equal(
            expected: string.Join(separator: Environment.NewLine, values: expectedLines) + Environment.NewLine,
            actual: actual
        );
    }

    private async Task<CommandLineResult> RunAsync(string commandLine)
    {
        string[] args = commandLine.Split(ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries);

        await using (StringWriter output = new())
        await using (StringWriter error = new())
        {
            try
            {
                int exitCode = await CommandLineRunner.RunAsync(
                    args: args,
                    configureServices: this.RegisterSubstitutes,
                    output: output,
                    error: error,
                    cancellationToken: this.CancellationToken()
                );

                return new(ExitCode: exitCode, Output: output.ToString(), Error: error.ToString());
            }
            finally
            {
                Environment.ExitCode = SUCCESS;
            }
        }
    }

    private void RegisterSubstitutes(IServiceCollection services)
    {
        services
            .AddSingleton(this._bulkCodeCleanUp)
            .AddSingleton(this._bulkDependencyReducer)
            .AddSingleton(this._bulkPackageUpdater)
            .AddSingleton(this._bulkTemplateUpdater)
            .AddSingleton(this._dependencyReducer)
            .AddSingleton(this._dotNetFilesDetector)
            .AddSingleton(this._gitRepositoryFactory)
            .AddSingleton(this._gitRepositoryListLoader);
    }

    [DebuggerDisplay("{ExitCode}")]
    private sealed record CommandLineResult(int ExitCode, string Output, string Error);
}
