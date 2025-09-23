using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Services;

public sealed class DotNetFilesDetectorTests : LoggingFolderCleanupTestBase
{
    private readonly IDotNetFilesDetector _dotNetFilesDetector;

    public DotNetFilesDetectorTests(ITestOutputHelper output)
        : base(output)
    {
        this._dotNetFilesDetector = new DotNetFilesDetector();
    }

    [Fact]
    public async Task ShouldNotHaveAnyFilesAsync()
    {
        DotNetFiles result = await this._dotNetFilesDetector.FindAsync(baseFolder: this.TempFolder, this.CancellationToken());
        Assert.Equal(expected: this.TempFolder, actual: result.SourceDirectory);
        Assert.False(condition: result.HasSolutions, userMessage: "Should not have solutions");
        Assert.False(condition: result.HasProjects, userMessage: "Should not have projects");
        Assert.False(condition: result.HasSolutionsAndProjects, userMessage: "Should not have projects");
    }

    [Fact]
    public Task ShouldHaveLegacySolutionFilesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        string baseFolder = Path.Combine(path1: this.TempFolder, path2: "src");
        Directory.CreateDirectory(baseFolder);

        string solution = Path.Combine(path1: baseFolder, path2: "Test.sln");
        string project = Path.Combine(path1: baseFolder, path2: "Test.csproj");

        return this.CheckShouldHaveSolutionFilesCommonAsync(sourceDirectory: baseFolder, solution: solution, project: project, cancellationToken: cancellationToken);
    }

    private async Task CheckShouldHaveSolutionFilesCommonAsync(string sourceDirectory, string solution, string project, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path: solution, contents: "Solution", cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(path: project, contents: "Project", cancellationToken: cancellationToken);
        DotNetFiles result = await this._dotNetFilesDetector.FindAsync(baseFolder: this.TempFolder, cancellationToken: cancellationToken);

        Assert.Equal(expected: sourceDirectory, actual: result.SourceDirectory);
        Assert.True(condition: result.HasSolutions, userMessage: "Should have solutions");
        Assert.True(condition: result.HasProjects, userMessage: "Should have projects");
        Assert.True(condition: result.HasSolutionsAndProjects, userMessage: "Should have solutions and projects");

        Assert.Equal([solution], actual: result.Solutions);
        Assert.Equal([project], actual: result.Projects);
    }

    [Fact]
    public Task ShouldHaveXmlSolutionFilesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        string baseFolder = Path.Combine(path1: this.TempFolder, path2: "src");
        Directory.CreateDirectory(baseFolder);

        string solution = Path.Combine(path1: baseFolder, path2: "Test.slnx");
        string project = Path.Combine(path1: baseFolder, path2: "Test.csproj");

        return this.CheckShouldHaveSolutionFilesCommonAsync(sourceDirectory: baseFolder, solution: solution, project: project, cancellationToken: cancellationToken);
    }

    [Fact]
    public async Task ShouldNotHaveDotNetWithoutSourceFolderAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await File.WriteAllTextAsync(Path.Combine(path1: this.TempFolder, path2: "Test.sln"), contents: "Solution", cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(path1: this.TempFolder, path2: "Test.csproj"), contents: "Solution", cancellationToken: cancellationToken);
        DotNetFiles result = await this._dotNetFilesDetector.FindAsync(baseFolder: this.TempFolder, cancellationToken: cancellationToken);
        Assert.Equal(expected: this.TempFolder, actual: result.SourceDirectory);
        Assert.False(condition: result.HasSolutions, userMessage: "Should not have solutions");
        Assert.False(condition: result.HasProjects, userMessage: "Should not have projects");
        Assert.False(condition: result.HasSolutionsAndProjects, userMessage: "Should not have projects");
    }
}