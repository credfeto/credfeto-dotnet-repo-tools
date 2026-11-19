using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Formatter.Setup;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests;

// Runs the real command line (parsing, help and exit codes) with the real services.
// ConsoleAppFramework keeps its state in statics, so every command-line test lives in this one class:
// xunit runs the tests of a class one at a time.
public sealed class CommandLineTests : LoggingFolderCleanupTestBase
{
    private const int SUCCESS = 0;
    private const int ERROR = 1;
    private const int HELP_SHOWN = 129;

    private const string UNFORMATTED_SOURCE = "public class Foo{public int Bar(){return 1;}}";

    public CommandLineTests(ITestOutputHelper output)
        : base(output) { }

    [Fact]
    public async Task FormatsEveryInputFileAsync()
    {
        string first = await this.CreateFileAsync("First.cs");
        string second = await this.CreateFileAsync("Second.cs");

        CommandLineResult result = await this.RunAsync([first, second]);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        Assert.NotEqual(
            expected: UNFORMATTED_SOURCE,
            actual: await this.ReadFileAsync(first),
            comparer: StringComparer.Ordinal
        );
        Assert.NotEqual(
            expected: UNFORMATTED_SOURCE,
            actual: await this.ReadFileAsync(second),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task FormatsAFolderWhenBuildRootIsGivenWithoutRemoveSuppressionsAsync()
    {
        string folder = Path.Combine(path1: this.TempFolder, path2: "Folder");
        string file = await this.CreateFileAsync("Folder/Foo.cs");

        CommandLineResult result = await this.RunAsync(["--build-root", this.TempFolder, folder]);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        Assert.NotEqual(
            expected: UNFORMATTED_SOURCE,
            actual: await this.ReadFileAsync(file),
            comparer: StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task SelfTestSucceedsWithoutInputsAsync()
    {
        CommandLineResult result = await this.RunAsync(["--self-test"]);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
    }

    [Fact]
    public async Task SelfTestFailsWhenStartupFailsAsync()
    {
        CommandLineResult result = await this.RunAsync(args: ["--self-test"], configureServices: static _ => { });

        Assert.Equal(expected: ERROR, actual: result.ExitCode);
    }

    [Fact]
    public async Task HelpListsArgumentsAndOptionsAsync()
    {
        CommandLineResult result = await this.RunAsync(["--help"]);

        Assert.Equal(expected: HELP_SHOWN, actual: result.ExitCode);
        AssertHelpIs(
            actual: result.Output,
            "Usage: Credfeto.DotNet.Repo.Formatter [--remove-suppressions] [--build-root <String>] [--self-test] [--help] [--version] inputs0 ... inputsN",
            "",
            "Format C# source files and project files",
            "",
            "Arguments:",
            "  0: inputs    Files, globs, or folders to process (Required)",
            "",
            "Options:",
            "  --remove-suppressions    Remove redundant [SuppressMessage] attributes (requires --build-root)",
            "  --build-root <String>    Root directory used for dotnet build when --remove-suppressions is enabled",
            "  --self-test              Check the tool can start, then exit without processing any files",
            "  -h, --help               Show help message",
            "  --version                Show version"
        );
    }

    [Fact]
    public async Task VersionShowsProductAndVersionAsync()
    {
        CommandLineResult result = await this.RunAsync(["--version"]);

        Assert.Equal(expected: SUCCESS, actual: result.ExitCode);
        Assert.StartsWith(
            expectedStartString: "C# Source and Project Formatter ",
            actualString: result.Output,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("--bogus")]
    [InlineData("--build-root")]
    [InlineData("--remove-suppressions|{dir}/Foo.cs")]
    [InlineData("--build-root|{dir}/does-not-exist|{dir}/Foo.cs")]
    [InlineData("{dir}/does-not-exist.cs")]
    [InlineData("{dir}/readme.txt")]
    public async Task BadArgumentsExitWithErrorAndLeaveTheFileAloneAsync(string commandLine)
    {
        string file = await this.CreateFileAsync("Foo.cs");
        await this.CreateFileAsync("readme.txt");
        string[] args = commandLine
            .Replace(oldValue: "{dir}", newValue: this.TempFolder, comparisonType: StringComparison.Ordinal)
            .Split('|', StringSplitOptions.RemoveEmptyEntries);

        CommandLineResult result = await this.RunAsync(args);

        Assert.Equal(expected: ERROR, actual: result.ExitCode);
        Assert.Equal(expected: UNFORMATTED_SOURCE, actual: await this.ReadFileAsync(file));
    }

    private static void AssertHelpIs(string actual, params IReadOnlyList<string> expectedLines)
    {
        Assert.Equal(
            expected: string.Join(separator: Environment.NewLine, values: expectedLines) + Environment.NewLine,
            actual: actual
        );
    }

    private Task<CommandLineResult> RunAsync(IReadOnlyList<string> args)
    {
        return this.RunAsync(args: args, configureServices: static services => services.AddServices());
    }

    private async Task<CommandLineResult> RunAsync(
        IReadOnlyList<string> args,
        Action<IServiceCollection> configureServices
    )
    {
        await using (StringWriter output = new())
        await using (StringWriter error = new())
        {
            try
            {
                int exitCode = await CommandLineRunner.RunAsync(
                    args: args,
                    configureServices: configureServices,
                    output: output,
                    error: error,
                    cancellationToken: this.CancellationToken()
                );

                return new(ExitCode: exitCode, Output: output.ToString());
            }
            finally
            {
                Environment.ExitCode = SUCCESS;
            }
        }
    }

    private async Task<string> CreateFileAsync(string relativePath)
    {
        string fullPath = Path.Combine(path1: this.TempFolder, path2: relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? this.TempFolder);

        await File.WriteAllTextAsync(
            path: fullPath,
            contents: UNFORMATTED_SOURCE,
            cancellationToken: this.CancellationToken()
        );

        return fullPath;
    }

    private Task<string> ReadFileAsync(string path)
    {
        return File.ReadAllTextAsync(path: path, cancellationToken: this.CancellationToken());
    }

    [DebuggerDisplay("{ExitCode}")]
    private sealed record CommandLineResult(int ExitCode, string Output);
}
