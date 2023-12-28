using System.IO;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using FunFair.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class GitRepositoryLocatorTests : LoggingTestBase
{
    private readonly string _basePath;
    private readonly IGitRepositoryLocator _gitRepositoryLocator;

    public GitRepositoryLocatorTests(ITestOutputHelper output)
        : base(output)
    {
        this._gitRepositoryLocator = new GitRepositoryLocator();

        this._basePath = Path.GetTempPath();
    }

    [Theory]
    [InlineData(Repositories.GitHubSelfHttps, "credfeto", "credfeto-dotnet-repo-tools")]
    [InlineData(Repositories.GitHubSelfSsh, "credfeto", "credfeto-dotnet-repo-tools")]
    [InlineData(Repositories.GitHubHttps, "credfeto", "scratch")]
    [InlineData(Repositories.GitHubSsh, "credfeto", "scratch")]
    [InlineData("https://github.com/meziantou/Meziantou.Analyzer.git", "meziantou", "Meziantou.Analyzer")]
    [InlineData("git@github.com:meziantou/Meziantou.Analyzer.git", "meziantou", "Meziantou.Analyzer")]
    public void GetFolderForRepo(string gitUrl, string expectedParent, string expectedSub)
    {
        string expected = Path.Combine(path1: this._basePath, path2: expectedParent, path3: expectedSub);
        string actual = this._gitRepositoryLocator.GetWorkingDirectory(workDir: this._basePath, repoUrl: gitUrl);

        this.Output.WriteLine($"Expected: {expected}");
        this.Output.WriteLine($"Actual  : {actual}");

        Assert.Equal(expected: expected, actual: actual);
    }
}