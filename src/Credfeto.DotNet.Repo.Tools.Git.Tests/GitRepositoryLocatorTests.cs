using System.IO;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
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
    [InlineData(Repositories.GitHubHttps, "credfeto", "credfeto-dotnet-repo-tools")]
    [InlineData(Repositories.GitHubSsh, "credfeto", "credfeto-dotnet-repo-tools")]
    public void GetFolderForRepo(string gitUrl, string expectedParent, string expectedSub)
    {
        string actual = this._gitRepositoryLocator.GetWorkingDirectory(workDir: this._basePath, repoUrl: gitUrl);
        string expected = Path.Combine(path1: this._basePath, path2: expectedParent, path3: expectedSub);

        Assert.Equal(expected: expected, actual: actual);
    }
}