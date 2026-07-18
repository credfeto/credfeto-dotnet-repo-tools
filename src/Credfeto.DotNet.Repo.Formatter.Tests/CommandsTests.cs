using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp;
using Credfeto.DotNet.Repo.Tools.Extensions;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests;

public sealed class CommandsTests : LoggingFolderCleanupTestBase
{
    private readonly Commands _commands;
    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IProjectXmlRewriter _projectXmlRewriter;
    private readonly IResharperSuppressionToSuppressMessage _resharperSuppressionToSuppressMessage;
    private readonly ISourceFileReformatter _sourceFileReformatter;
    private readonly ISourceFileSuppressionRemover _sourceFileSuppressionRemover;
    private readonly IXmlDocCommentRemover _xmlDocCommentRemover;

    public CommandsTests(ITestOutputHelper output)
        : base(output)
    {
        this._projectXmlRewriter = GetSubstitute<IProjectXmlRewriter>();
        this._sourceFileReformatter = GetSubstitute<ISourceFileReformatter>();
        this._xmlDocCommentRemover = GetSubstitute<IXmlDocCommentRemover>();
        this._resharperSuppressionToSuppressMessage = GetSubstitute<IResharperSuppressionToSuppressMessage>();
        this._sourceFileSuppressionRemover = GetSubstitute<ISourceFileSuppressionRemover>();
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        this._dotNetFilesDetector = GetSubstitute<IDotNetFilesDetector>();

        this.SetupPassThroughCsCleaners();

        this._commands = new Commands(
            projectXmlRewriter: this._projectXmlRewriter,
            sourceFileReformatter: this._sourceFileReformatter,
            xmlDocCommentRemover: this._xmlDocCommentRemover,
            resharperSuppressionToSuppressMessage: this._resharperSuppressionToSuppressMessage,
            sourceFileSuppressionRemover: this._sourceFileSuppressionRemover,
            dotNetBuild: this._dotNetBuild,
            dotNetFilesDetector: this._dotNetFilesDetector,
            logger: this.GetTypedLogger<Commands>()
        );
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2012: Use ValueTasks correctly",
        Justification = "NSubstitute mock setup requires calling async methods without awaiting"
    )]
    private void SetupPassThroughCsCleaners()
    {
        this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0));
        this._xmlDocCommentRemover.RemoveXmlDocComments(Arg.Any<string>()).Returns(x => x.ArgAt<string>(0));
        this._sourceFileReformatter.ReformatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(x => ValueTask.FromResult(x.ArgAt<string>(1)));
        this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<BuildContext>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(x => ValueTask.FromResult(x.ArgAt<string>(1)));
    }

    private void SetupBuildRoot(string buildRoot, IReadOnlyList<string> projects)
    {
        this._dotNetFilesDetector.FindAsync(baseFolder: buildRoot, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new DotNetFiles(SourceDirectory: buildRoot, Solutions: [], Projects: projects));
        this._dotNetBuild.LoadBuildSettingsAsync(
                projects: Arg.Any<IReadOnlyList<string>>(),
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new BuildSettings(PublishableProjects: [], PackableProjects: [], Framework: null));
    }

    private async Task<string> CreateFileAsync(string relativePath, string content)
    {
        string fullPath = Path.Combine(path1: this.TempFolder, path2: relativePath);
        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path: fullPath, contents: content, cancellationToken: this.CancellationToken());

        return fullPath;
    }

    private Task<string> ReadFileAsync(string path)
    {
        return File.ReadAllTextAsync(path: path, cancellationToken: this.CancellationToken());
    }

    private ValueTask<string> ReceivedRemoveSuppressionsAsync(int times)
    {
        return this
            ._sourceFileSuppressionRemover.ReceivedWithAnyArgs(times)
            .RemoveSuppressionsAsync(string.Empty, string.Empty, default, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CleanupAsyncReturnsErrorWhenRemoveSuppressionsWithoutBuildRootAsync(string? buildRoot)
    {
        int result = await this._commands.CleanupAsync(
            inputs: ["file.cs"],
            removeSuppressions: true,
            buildRoot: buildRoot
        );

        Assert.Equal(expected: ExitCodes.Error, actual: result);
        await this._dotNetFilesDetector.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncReturnsErrorWhenBuildRootDoesNotExistAsync()
    {
        string missingBuildRoot = Path.Combine(path1: this.TempFolder, path2: "does-not-exist");

        int result = await this._commands.CleanupAsync(
            inputs: ["file.cs"],
            removeSuppressions: false,
            buildRoot: missingBuildRoot
        );

        Assert.Equal(expected: ExitCodes.Error, actual: result);
        await this._dotNetFilesDetector.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncReturnsErrorWhenNoInputFilesAsync()
    {
        int result = await this._commands.CleanupAsync(inputs: []);

        Assert.Equal(expected: ExitCodes.Error, actual: result);
    }

    [Fact]
    public async Task CleanupAsyncReturnsErrorWhenGlobMatchesNoFilesAsync()
    {
        string glob = Path.Combine(path1: this.TempFolder, path2: "*.doesnotexist");

        int result = await this._commands.CleanupAsync(inputs: [glob]);

        Assert.Equal(expected: ExitCodes.Error, actual: result);
    }

    [Fact]
    public async Task CleanupAsyncReturnsErrorForUnsupportedFileTypeAsync()
    {
        string file = await this.CreateFileAsync(relativePath: "readme.txt", content: "hello");

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Error, actual: result);
        await this
            ._sourceFileReformatter.DidNotReceive()
            .ReformatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncProcessesExplicitCSharpFileAsync()
    {
        string file = await this.CreateFileAsync(relativePath: "Foo.cs", content: "public class Foo { }");

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.Equal(expected: "public class Foo { }", actual: await this.ReadFileAsync(file));
    }

    [Fact]
    public async Task CleanupAsyncWritesUpdatedCSharpFileContentAsync()
    {
        string file = await this.CreateFileAsync(
            relativePath: "Foo.cs",
            content: "// resharper disable\npublic class Foo { }"
        );
        this._resharperSuppressionToSuppressMessage.Replace(Arg.Any<string>()).Returns("public class Foo { }");

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.Equal(expected: "public class Foo { }", actual: await this.ReadFileAsync(file));
    }

    [Fact]
    public async Task CleanupAsyncDeduplicatesRepeatedInputsAsync()
    {
        string file = await this.CreateFileAsync(relativePath: "Foo.cs", content: "public class Foo { }");

        int result = await this._commands.CleanupAsync(inputs: [file, file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this
            ._sourceFileReformatter.Received(1)
            .ReformatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncProcessesDirectoryInputRecursivelyExcludingGeneratedFilesAsync()
    {
        await this.CreateFileAsync(relativePath: Path.Combine("src", "Foo.cs"), content: "public class Foo { }");
        await this.CreateFileAsync(
            relativePath: Path.Combine("obj", "Debug", "Foo.cs"),
            content: "public class Foo2 { }"
        );
        await this.CreateFileAsync(relativePath: Path.Combine("generated", "Bar.cs"), content: "public class Bar { }");
        await this.CreateFileAsync(relativePath: "Baz.generated.cs", content: "public class Baz { }");
        await this.CreateFileAsync(relativePath: "readme.txt", content: "hello");

        int result = await this._commands.CleanupAsync(inputs: [this.TempFolder]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this
            ._sourceFileReformatter.Received(1)
            .ReformatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncProcessesGlobInputAsync()
    {
        await this.CreateFileAsync(relativePath: "Foo.cs", content: "public class Foo { }");
        await this.CreateFileAsync(relativePath: "readme.txt", content: "hello");
        string glob = Path.Combine(path1: this.TempFolder, path2: "*.cs");

        int result = await this._commands.CleanupAsync(inputs: [glob]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this
            ._sourceFileReformatter.Received(1)
            .ReformatAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncProcessesUnchangedProjectFileAsync()
    {
        const string projectXml = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>";
        string file = await this.CreateFileAsync(relativePath: "Test.csproj", content: projectXml);

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.Equal(expected: projectXml, actual: await this.ReadFileAsync(file));
    }

    [Fact]
    public async Task CleanupAsyncRewritesProjectFileWhenPropertyGroupsReorderedAsync()
    {
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        string file = await this.CreateFileAsync(relativePath: "Test.csproj", content: projectXml);
        this._projectXmlRewriter.ReOrderPropertyGroups(Arg.Any<XmlDocument>(), Arg.Any<string>()).Returns(true);

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.NotEqual(expected: projectXml, actual: await this.ReadFileAsync(file));
    }

    [Fact]
    public async Task CleanupAsyncRewritesProjectFileWhenIncludesReorderedAsync()
    {
        const string projectXml =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"b.cs\" /><Compile Include=\"a.cs\" /></ItemGroup></Project>";
        string file = await this.CreateFileAsync(relativePath: "Test.csproj", content: projectXml);
        this._projectXmlRewriter.ReOrderIncludes(Arg.Any<XmlDocument>(), Arg.Any<string>()).Returns(true);

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.NotEqual(expected: projectXml, actual: await this.ReadFileAsync(file));
    }

    [Fact]
    public async Task CleanupAsyncDoesNotRewriteProjectFileWhenAlreadyCanonicalAsync()
    {
        const string rawXml = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup></PropertyGroup></Project>";
        XmlDocument document = new();
        document.LoadXml(rawXml);
        string canonicalXml = await ProjectXmlSerializer.ToProjectFileTextAsync(
            document: document,
            cancellationToken: this.CancellationToken()
        );
        string file = await this.CreateFileAsync(relativePath: "Test.csproj", content: canonicalXml);
        this._projectXmlRewriter.ReOrderPropertyGroups(Arg.Any<XmlDocument>(), Arg.Any<string>()).Returns(true);

        int result = await this._commands.CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.Equal(expected: canonicalXml, actual: await this.ReadFileAsync(file));
    }

    [Fact]
    public async Task CleanupAsyncRemovesSuppressionsWhenBuildRootConfiguredAsync()
    {
        string buildRoot = this.TempFolder;
        string project = await this.CreateFileAsync(
            relativePath: "Test.csproj",
            content: "<Project Sdk=\"Microsoft.NET.Sdk\" />"
        );
        string csFile = await this.CreateFileAsync(relativePath: "Foo.cs", content: "public class Foo { }");
        this.SetupBuildRoot(buildRoot: buildRoot, projects: [project]);

        int result = await this._commands.CleanupAsync(
            inputs: [csFile],
            removeSuppressions: true,
            buildRoot: buildRoot
        );

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this.ReceivedRemoveSuppressionsAsync(1);
        await this
            ._dotNetFilesDetector.Received(1)
            .FindAsync(baseFolder: buildRoot, cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CleanupAsyncDoesNotRemoveSuppressionsWithoutBuildRootAsync()
    {
        string csFile = await this.CreateFileAsync(relativePath: "Foo.cs", content: "public class Foo { }");

        int result = await this._commands.CleanupAsync(inputs: [csFile]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this.ReceivedRemoveSuppressionsAsync(0);
    }

    [Fact]
    public async Task CleanupAsyncProcessesProjectFileWithoutUsingBuildContextForSuppressionsAsync()
    {
        string buildRoot = this.TempFolder;
        string project = await this.CreateFileAsync(
            relativePath: "Test.csproj",
            content: "<Project Sdk=\"Microsoft.NET.Sdk\" />"
        );
        this.SetupBuildRoot(buildRoot: buildRoot, projects: [project]);
        this._projectXmlRewriter.ReOrderPropertyGroups(Arg.Any<XmlDocument>(), Arg.Any<string>()).Returns(true);

        int result = await this._commands.CleanupAsync(
            inputs: [project],
            removeSuppressions: true,
            buildRoot: buildRoot
        );

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this.ReceivedRemoveSuppressionsAsync(0);
    }

    [Fact]
    public async Task CleanupAsyncAllowsBuildRootWhenRemoveSuppressionsDisabledAsync()
    {
        string buildRoot = this.TempFolder;
        string csFile = await this.CreateFileAsync(relativePath: "Foo.cs", content: "public class Foo { }");

        int result = await this._commands.CleanupAsync(
            inputs: [csFile],
            removeSuppressions: false,
            buildRoot: buildRoot
        );

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        await this._dotNetFilesDetector.DidNotReceive().FindAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
