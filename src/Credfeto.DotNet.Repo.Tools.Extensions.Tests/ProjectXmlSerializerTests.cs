using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Extensions.Tests;

public sealed class ProjectXmlSerializerTests : LoggingTestBase, IDisposable
{
    private readonly string _baseFolder;

    public ProjectXmlSerializerTests(ITestOutputHelper output)
        : base(output)
    {
        this._baseFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._baseFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(this._baseFolder))
        {
            Directory.Delete(path: this._baseFolder, recursive: true);
        }
    }

    private static XmlDocument LoadProject(string xml)
    {
        XmlDocument document = new();
        document.LoadXml(xml);

        return document;
    }

    [Fact]
    public async Task ToProjectFileTextAsyncEndsWithExactlyOneTrailingNewLine()
    {
        XmlDocument document = LoadProject(
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>"
        );

        string result = await ProjectXmlSerializer.ToProjectFileTextAsync(
            document: document,
            cancellationToken: this.CancellationToken()
        );

        Assert.EndsWith(expectedEndString: "</Project>\n", actualString: result, StringComparison.Ordinal);
        Assert.False(result.EndsWith("\n\n", StringComparison.Ordinal), "Should not have more than one trailing newline");
    }

    [Fact]
    public async Task ToProjectFileTextAsyncAfterRemovingNodeStillEndsWithExactlyOneTrailingNewLine()
    {
        XmlDocument document = LoadProject(
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                + "  <ItemGroup>\n"
                + "    <PackageReference Include=\"Foo\" Version=\"1.0.0\" />\n"
                + "    <PackageReference Include=\"Bar\" Version=\"2.0.0\" />\n"
                + "  </ItemGroup>\n"
                + "</Project>"
        );

        XmlNode? node = document.SelectSingleNode("//PackageReference[@Include='Bar']");

        // ! Node is guaranteed to exist and have a parent given the document above
        node!.ParentNode!.RemoveChild(node);

        string result = await ProjectXmlSerializer.ToProjectFileTextAsync(
            document: document,
            cancellationToken: this.CancellationToken()
        );

        Assert.EndsWith(expectedEndString: "</Project>\n", actualString: result, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "Bar", actualString: result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToProjectFileTextAsyncUsesTwoSpaceIndentAndOmitsXmlDeclaration()
    {
        XmlDocument document = LoadProject(
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"
        );

        string result = await ProjectXmlSerializer.ToProjectFileTextAsync(
            document: document,
            cancellationToken: this.CancellationToken()
        );

        const string expected = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n";

        Assert.Equal(expected: expected, actual: result);
    }

    [Fact]
    public async Task WriteAsyncWritesFileWithBomPreamble()
    {
        string path = Path.Combine(this._baseFolder, "test.csproj");

        await ProjectXmlSerializer.WriteAsync(
            filePath: path,
            content: "<Project />\n",
            cancellationToken: this.CancellationToken()
        );

        byte[] bytes = await File.ReadAllBytesAsync(path: path, cancellationToken: this.CancellationToken());
        byte[] preamble = Encoding.UTF8.GetPreamble();

        Assert.Equal(expected: preamble[0], actual: bytes[0]);
        Assert.Equal(expected: preamble[1], actual: bytes[1]);
        Assert.Equal(expected: preamble[2], actual: bytes[2]);
    }

    [Fact]
    public async Task SaveAsyncWritesProjectFileTextWithSingleTrailingNewLineToDisk()
    {
        XmlDocument document = LoadProject("<Project Sdk=\"Microsoft.NET.Sdk\" />");
        string path = Path.Combine(this._baseFolder, "test.csproj");

        await ProjectXmlSerializer.SaveAsync(
            document: document,
            filePath: path,
            cancellationToken: this.CancellationToken()
        );

        string content = await File.ReadAllTextAsync(path: path, cancellationToken: this.CancellationToken());

        Assert.EndsWith(expectedEndString: "\n", actualString: content, StringComparison.Ordinal);
        Assert.False(content.EndsWith("\n\n", StringComparison.Ordinal), "Should not have more than one trailing newline");
    }
}
