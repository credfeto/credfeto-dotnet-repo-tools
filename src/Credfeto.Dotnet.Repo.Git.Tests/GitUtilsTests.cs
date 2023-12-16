using System.Collections.Generic;
using FunFair.Test.Common;
using LibGit2Sharp;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.Dotnet.Repo.Git.Tests;

public sealed class GitUtilsTests : LoggingFolderCleanupTestBase
{
    public GitUtilsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Theory]
    [InlineData("https://github.com/credfeto/credfeto-dotnet-repo-tools", "credfeto-dotnet-repo-tools")]
    [InlineData("git@github.com:credfeto/credfeto-dotnet-repo-tools.git", "credfeto-dotnet-repo-tools")]
    public void GetFolderForRepo(string gitUrl, string expected)
    {
        string actual = GitUtils.GetFolderForRepo(gitUrl);

        Assert.Equal(expected: expected, actual: actual);
    }

    [Fact(Skip = "No SSH Support yet")]
    public void CanCloneSsh()
    {
        using (Repository repo = GitUtils.OpenOrClone(workDir: this.TempFolder, repoUrl: "git@github.com:credfeto/scratch.git"))
        {
            this.Output.WriteLine($"Repo: {repo.Info.Path}");
        }
    }

    [Fact]
    public void CanCloneHttps()
    {
        using (Repository repo = GitUtils.OpenOrClone(workDir: this.TempFolder, repoUrl: "https://github.com/credfeto/scratch.git"))
        {
            this.Output.WriteLine($"Repo: {repo.Info.Path}");

            string branch = GitUtils.GetDefaultBranch(repo);
            this.Output.WriteLine($"Default Branch: {branch}");

            IReadOnlyCollection<string> remoteBranches = GitUtils.GetRemoteBranches(repo);

            foreach (string remoteBranch in remoteBranches)
            {
                this.Output.WriteLine($"* Branch: {remoteBranch}");
            }
        }
    }
}