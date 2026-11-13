using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Formatter.Constants;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using Xunit;

namespace Credfeto.DotNet.Repo.Formatter.Tests;

public sealed partial class CommandsTests
{
    private const string LF = "\n";
    private const string CRLF = "\r\n";

    private static readonly string[] UnformattedSourceLines =
    [
        "namespace Example;",
        string.Empty,
        "public class Foo{public int Bar(){return 1;}}",
    ];

    private static readonly string[] FormattedSourceLines =
    [
        "namespace Example;",
        string.Empty,
        "public class Foo",
        "{",
        "    public int Bar()",
        "    {",
        "        return 1;",
        "    }",
        "}",
    ];

    private static readonly string[] UnorderedProjectLines =
    [
        "<Project Sdk=\"Microsoft.NET.Sdk\">",
        "  <ItemGroup>",
        "    <PackageReference Include=\"Beta\" Version=\"1.0.0\" />",
        "    <PackageReference Include=\"Alpha\" Version=\"1.0.0\" />",
        "  </ItemGroup>",
        "</Project>",
    ];

    private static readonly string[] OrderedProjectLines =
    [
        "<Project Sdk=\"Microsoft.NET.Sdk\">",
        "  <ItemGroup>",
        "    <PackageReference Include=\"Alpha\" Version=\"1.0.0\" />",
        "    <PackageReference Include=\"Beta\" Version=\"1.0.0\" />",
        "  </ItemGroup>",
        "</Project>",
    ];

    private Commands CreateCommandsWithRealCleaners()
    {
        return new(
            projectXmlRewriter: new ProjectXmlRewriter(this.GetTypedLogger<ProjectXmlRewriter>()),
            sourceFileReformatter: new SourceFileReformatter(this.GetTypedLogger<SourceFileReformatter>()),
            xmlDocCommentRemover: new XmlDocCommentRemover(),
            resharperSuppressionToSuppressMessage: new ResharperSuppressionToSuppressMessage(),
            sourceFileSuppressionRemover: this._sourceFileSuppressionRemover,
            dotNetBuild: this._dotNetBuild,
            dotNetFilesDetector: this._dotNetFilesDetector,
            coconaAppContextAccessor: this._coconaAppContextAccessor,
            logger: this.GetTypedLogger<Commands>()
        );
    }

    private static string JoinLines(string[] lines, string newLine)
    {
        return string.Join(separator: newLine, value: lines) + newLine;
    }

    private static string ReplaceFirstNewLine(string content, string oldNewLine, string newNewLine)
    {
        int position = content.IndexOf(value: oldNewLine, comparisonType: StringComparison.Ordinal);

        return content[..position] + newNewLine + content[(position + oldNewLine.Length)..];
    }

    private async Task WriteEditorConfigAsync(string? endOfLine)
    {
        if (endOfLine is null)
        {
            return;
        }

        await this.CreateFileAsync(
            relativePath: ".editorconfig",
            content: "root = true\n\n[*.{cs,csproj}]\nend_of_line = " + endOfLine + "\n"
        );
    }

    private async Task<string> CleanupWithRealCleanersAsync(string relativePath, string content, string? endOfLine)
    {
        await this.WriteEditorConfigAsync(endOfLine);
        string file = await this.CreateFileAsync(relativePath: relativePath, content: content);

        int result = await this.CreateCommandsWithRealCleaners().CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);

        return file;
    }

    private async Task AssertCleanupLeavesFileUntouchedAsync(string relativePath, string content, string? endOfLine)
    {
        await this.WriteEditorConfigAsync(endOfLine);
        string file = await this.CreateFileAsync(relativePath: relativePath, content: content);
        byte[] bytesBefore = await File.ReadAllBytesAsync(path: file, cancellationToken: this.CancellationToken());
        DateTime lastWriteBefore = File.GetLastWriteTimeUtc(file);

        int result = await this.CreateCommandsWithRealCleaners().CleanupAsync(inputs: [file]);

        Assert.Equal(expected: ExitCodes.Success, actual: result);
        Assert.Equal(
            expected: bytesBefore,
            actual: await File.ReadAllBytesAsync(path: file, cancellationToken: this.CancellationToken())
        );
        Assert.Equal(expected: lastWriteBefore, actual: File.GetLastWriteTimeUtc(file));
    }

    [Theory]
    [InlineData(LF, null)]
    [InlineData(LF, "crlf")]
    [InlineData(CRLF, null)]
    [InlineData(CRLF, "lf")]
    public async Task CleanupAsyncKeepsLineEndingsOfCSharpFileThatNeedsFormattingAsync(
        string newLine,
        string? editorConfigEndOfLine
    )
    {
        string file = await this.CleanupWithRealCleanersAsync(
            relativePath: "Foo.cs",
            content: JoinLines(lines: UnformattedSourceLines, newLine: newLine),
            endOfLine: editorConfigEndOfLine
        );

        Assert.Equal(
            expected: JoinLines(lines: FormattedSourceLines, newLine: newLine),
            actual: await this.ReadFileAsync(file)
        );
    }

    [Theory]
    [InlineData(LF, CRLF, "crlf")]
    [InlineData(CRLF, LF, "lf")]
    public async Task CleanupAsyncNormalisesCSharpFileThatNeedsFormattingToItsDominantLineEndingAsync(
        string dominantNewLine,
        string firstNewLine,
        string editorConfigEndOfLine
    )
    {
        string content = ReplaceFirstNewLine(
            content: JoinLines(lines: UnformattedSourceLines, newLine: dominantNewLine),
            oldNewLine: dominantNewLine,
            newNewLine: firstNewLine
        );

        string file = await this.CleanupWithRealCleanersAsync(
            relativePath: "Foo.cs",
            content: content,
            endOfLine: editorConfigEndOfLine
        );

        Assert.Equal(
            expected: JoinLines(lines: FormattedSourceLines, newLine: dominantNewLine),
            actual: await this.ReadFileAsync(file)
        );
    }

    [Theory]
    [InlineData(LF, null)]
    [InlineData(LF, "crlf")]
    [InlineData(CRLF, null)]
    [InlineData(CRLF, "lf")]
    public Task CleanupAsyncLeavesCSharpFileThatNeedsNoFormattingByteForByteUnchangedAsync(
        string newLine,
        string? editorConfigEndOfLine
    )
    {
        return this.AssertCleanupLeavesFileUntouchedAsync(
            relativePath: "Foo.cs",
            content: JoinLines(lines: FormattedSourceLines, newLine: newLine),
            endOfLine: editorConfigEndOfLine
        );
    }

    [Theory]
    [InlineData(LF, null)]
    [InlineData(LF, "crlf")]
    [InlineData(CRLF, null)]
    [InlineData(CRLF, "lf")]
    public async Task CleanupAsyncKeepsLineEndingsOfProjectFileThatNeedsReorderingAsync(
        string newLine,
        string? editorConfigEndOfLine
    )
    {
        string file = await this.CleanupWithRealCleanersAsync(
            relativePath: "Test.csproj",
            content: JoinLines(lines: UnorderedProjectLines, newLine: newLine),
            endOfLine: editorConfigEndOfLine
        );

        Assert.Equal(
            expected: JoinLines(lines: OrderedProjectLines, newLine: newLine),
            actual: await this.ReadFileAsync(file)
        );
    }

    [Theory]
    [InlineData(LF, null)]
    [InlineData(LF, "crlf")]
    [InlineData(CRLF, null)]
    [InlineData(CRLF, "lf")]
    public Task CleanupAsyncLeavesProjectFileThatNeedsNoReorderingByteForByteUnchangedAsync(
        string newLine,
        string? editorConfigEndOfLine
    )
    {
        return this.AssertCleanupLeavesFileUntouchedAsync(
            relativePath: "Test.csproj",
            content: JoinLines(lines: OrderedProjectLines, newLine: newLine),
            endOfLine: editorConfigEndOfLine
        );
    }
}
