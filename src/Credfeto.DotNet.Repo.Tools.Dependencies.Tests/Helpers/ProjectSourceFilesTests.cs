using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests.Helpers;

public sealed class ProjectSourceFilesTests : TestBase, IDisposable
{
    private readonly string _projectFolder;

    public ProjectSourceFilesTests()
    {
        this._projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._projectFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(this._projectFolder))
        {
            Directory.Delete(path: this._projectFolder, recursive: true);
        }
    }

    [Theory]
    [InlineData("obj")]
    [InlineData("bin")]
    [InlineData("OBJ")]
    [InlineData("Bin")]
    public void IsBuildOutputShouldBeTrueForFileDirectlyInBuildOutputFolder(string folderName)
    {
        string file = Path.Combine(this._projectFolder, folderName, "Generated.cs");

        Assert.True(
            ProjectSourceFiles.IsBuildOutput(projectFolder: this._projectFolder, file: file),
            userMessage: "File directly in a build output folder should be treated as build output"
        );
    }

    [Theory]
    [InlineData("obj", "Release", "net10.0")]
    [InlineData("bin", "Debug", "net10.0")]
    [InlineData("Sub", "obj", "Debug")]
    [InlineData("Sub", "Deeper", "bin")]
    public void IsBuildOutputShouldBeTrueForFileNestedUnderBuildOutputFolder(string first, string second, string third)
    {
        string file = Path.Combine(this._projectFolder, first, second, third, "Generated.cs");

        Assert.True(
            ProjectSourceFiles.IsBuildOutput(projectFolder: this._projectFolder, file: file),
            userMessage: "File nested under a build output folder should be treated as build output"
        );
    }

    [Theory]
    [InlineData("obj.cs")]
    [InlineData("bin.cs")]
    [InlineData("Program.cs")]
    public void IsBuildOutputShouldBeFalseForFileInProjectRoot(string fileName)
    {
        string file = Path.Combine(this._projectFolder, fileName);

        Assert.False(
            ProjectSourceFiles.IsBuildOutput(projectFolder: this._projectFolder, file: file),
            userMessage: "File in the project root should not be treated as build output"
        );
    }

    [Theory]
    [InlineData("objects")]
    [InlineData("binary")]
    [InlineData("Services")]
    [InlineData("myobj")]
    public void IsBuildOutputShouldBeFalseForFolderMerelyContainingBuildOutputName(string folderName)
    {
        string file = Path.Combine(this._projectFolder, folderName, "Code.cs");

        Assert.False(
            ProjectSourceFiles.IsBuildOutput(projectFolder: this._projectFolder, file: file),
            userMessage: "Folder whose name only contains obj or bin should not be treated as build output"
        );
    }

    [Fact]
    public void IsBuildOutputShouldIgnoreBuildOutputNamesAboveProjectFolder()
    {
        string projectFolder = Path.Combine(this._projectFolder, "bin", "obj", "Project");
        string file = Path.Combine(projectFolder, "Code.cs");

        Assert.False(
            ProjectSourceFiles.IsBuildOutput(projectFolder: projectFolder, file: file),
            userMessage: "Folders above the project folder should not affect the build output check"
        );
    }

    [Fact]
    public async Task FindShouldExcludeFilesUnderObjAndBinFoldersAsync()
    {
        string program = await this.CreateFileAsync("Program.cs");
        string nested = await this.CreateFileAsync("Services", "Service.cs");
        await this.CreateFileAsync("obj", "Debug", "net10.0", "AssemblyInfo.cs");
        await this.CreateFileAsync("bin", "Release", "Generated.cs");
        await this.CreateFileAsync("Sub", "OBJ", "Nested.cs");
        await this.CreateFileAsync("Readme.txt");

        IReadOnlyList<string> files = ProjectSourceFiles.Find(
            projectFolder: this._projectFolder,
            searchPattern: "*.cs"
        );

        Assert.Equal(expected: 2, actual: files.Count);
        Assert.Contains(expected: program, collection: files, comparer: StringComparer.Ordinal);
        Assert.Contains(expected: nested, collection: files, comparer: StringComparer.Ordinal);
    }

    [Fact]
    public async Task FindShouldReturnEmptyWhenOnlyBuildOutputMatchesAsync()
    {
        await this.CreateFileAsync("obj", "Views", "Index.cshtml");
        await this.CreateFileAsync("bin", "Pages", "Page.razor");

        Assert.Empty(ProjectSourceFiles.Find(projectFolder: this._projectFolder, searchPattern: "*.cshtml"));
        Assert.Empty(ProjectSourceFiles.Find(projectFolder: this._projectFolder, searchPattern: "*.razor"));
    }

    [Fact]
    public void FindShouldReturnEmptyWhenNoFilesMatch()
    {
        Assert.Empty(ProjectSourceFiles.Find(projectFolder: this._projectFolder, searchPattern: "*.cs"));
    }

    private async Task<string> CreateFileAsync(params string[] relativeParts)
    {
        string file = Path.Combine([this._projectFolder, .. relativeParts]);
        string? folder = Path.GetDirectoryName(file);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await File.WriteAllTextAsync(path: file, contents: string.Empty, cancellationToken: this.CancellationToken());

        return file;
    }
}
