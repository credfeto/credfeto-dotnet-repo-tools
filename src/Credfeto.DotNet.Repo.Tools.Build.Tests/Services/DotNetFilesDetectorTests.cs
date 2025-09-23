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
        this._dotNetFilesDetector = new DotNetFilesDetector(new ProjectFinder());
    }

    [Fact]
    public async Task ShouldNotHaveAnyFilesAsync()
    {
        DotNetFiles? result = await this._dotNetFilesDetector.FindAsync(baseFolder: this.TempFolder, this.CancellationToken());
        Assert.Null(result);
    }

    [Fact]
    public Task ShouldHaveLegacySolutionFilesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        string baseFolder = Path.Combine(path1: this.TempFolder, path2: "src");
        Directory.CreateDirectory(baseFolder);

        string solution = Path.Combine(path1: baseFolder, path2: "Test.sln");
        string project = Path.Combine(path1: baseFolder, path2: "Test.csproj");

        return this.CheckShouldHaveSolutionFilesCommonAsync(solution: solution, project: project, cancellationToken: cancellationToken);
    }

    private async Task CheckShouldHaveSolutionFilesCommonAsync(string solution, string project, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path: solution, contents: "Solution", cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(path: project, contents: "Project", cancellationToken: cancellationToken);
        DotNetFiles? result = await this._dotNetFilesDetector.FindAsync(baseFolder: this.TempFolder, cancellationToken: cancellationToken);
        Assert.NotNull(result);

        Assert.Equal([solution], actual: result.Value.Solutions);
        Assert.Equal([project], actual: result.Value.Projects);
    }

    [Fact]
    public Task ShouldHaveXmlSolutionFilesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        string baseFolder = Path.Combine(path1: this.TempFolder, path2: "src");
        Directory.CreateDirectory(baseFolder);

        string solution = Path.Combine(path1: baseFolder, path2: "Test.slnx");
        string project = Path.Combine(path1: baseFolder, path2: "Test.csproj");

        return this.CheckShouldHaveSolutionFilesCommonAsync(solution: solution, project: project, cancellationToken: cancellationToken);
    }

    [Fact]
    public async Task ShouldNotHaveDotNetWithoutSourceFolderAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await File.WriteAllTextAsync(Path.Combine(path1: this.TempFolder, path2: "Test.sln"), contents: "Solution", cancellationToken: cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(path1: this.TempFolder, path2: "Test.csproj"), contents: "Solution", cancellationToken: cancellationToken);
        DotNetFiles? result = await this._dotNetFilesDetector.FindAsync(baseFolder: this.TempFolder, cancellationToken: cancellationToken);
        Assert.Null(result);
    }
}