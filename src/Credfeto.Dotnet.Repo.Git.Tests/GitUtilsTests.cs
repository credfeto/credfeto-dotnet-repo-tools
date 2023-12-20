using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public Task CanCloneSshAsync()
    {
        return this.CloneTestCommonAsync("git@github.com:credfeto/scratch.git", CancellationToken.None);
    }

    [Fact]
    public Task CanCloneHttpsAsync()
    {
        return this.CloneTestCommonAsync("https://github.com/credfeto/scratch.git", CancellationToken.None);
    }

    private async Task CloneTestCommonAsync(string uri, CancellationToken cancellationToken)
    {
        using (Repository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken))
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

        using (Repository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken))
        {
            GitUtils.RemoveAllLocalBranches(repo);
        }
    }
}