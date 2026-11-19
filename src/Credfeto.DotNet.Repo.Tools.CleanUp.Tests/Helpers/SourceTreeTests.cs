using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.CleanUp.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Helpers;

public sealed class SourceTreeTests : LoggingFolderCleanupTestBase
{
    public SourceTreeTests(ITestOutputHelper output)
        : base(output) { }

    private async Task<string> CreateFileAsync(params string[] relativePath)
    {
        string fullPath = Path.Combine([this.TempFolder, .. relativePath]);
        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            path: fullPath,
            contents: string.Empty,
            cancellationToken: this.CancellationToken()
        );

        return fullPath;
    }

    private IReadOnlyList<string> Enumerate(params string[] searchPatterns)
    {
        return
        [
            .. SourceTree
                .EnumerateFiles(directory: this.TempFolder, searchPatterns: searchPatterns)
                .Order(StringComparer.Ordinal),
        ];
    }

    [Fact]
    public async Task EnumerateFilesShouldSkipGitBinAndObjFoldersAsync()
    {
        string included = await this.CreateFileAsync("src", "Foo.cs");
        await this.CreateFileAsync(".git", "objects", "Foo.cs");
        await this.CreateFileAsync("bin", "Debug", "Foo.cs");
        await this.CreateFileAsync("src", "obj", "Debug", "Foo.cs");

        IReadOnlyList<string> files = this.Enumerate("*.cs");

        Assert.Equal(expected: [included], actual: files);
    }

    [Fact]
    public async Task EnumerateFilesShouldReturnOnlyFilesMatchingAnyPatternAsync()
    {
        string project = await this.CreateFileAsync("src", "Foo.csproj");
        string source = await this.CreateFileAsync("src", "Foo.cs");
        await this.CreateFileAsync("src", "readme.txt");

        IReadOnlyList<string> files = this.Enumerate("*.cs", "*.csproj");

        Assert.Equal(expected: [source, project], actual: files);
    }

    [Fact]
    public async Task EnumerateFilesShouldMatchPatternsIgnoringCaseAsync()
    {
        string upper = await this.CreateFileAsync("Schema.SQL");

        IReadOnlyList<string> files = this.Enumerate("*.sql");

        Assert.Equal(expected: [upper], actual: files);
    }

    [Fact]
    public async Task EnumerateFilesShouldIncludeFilesInHiddenFoldersAsync()
    {
        string hidden = await this.CreateFileAsync(".config", "Foo.cs");

        IReadOnlyList<string> files = this.Enumerate("*.cs");

        Assert.Equal(expected: [hidden], actual: files);
    }
}
