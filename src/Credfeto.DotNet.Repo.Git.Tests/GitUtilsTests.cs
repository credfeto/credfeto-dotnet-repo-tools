using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FunFair.Test.Common;
using LibGit2Sharp;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Git.Tests;

public sealed class GitUtilsTests : LoggingFolderCleanupTestBase
{
    private const string REPO_HTTPS = "https://github.com/credfeto/scratch.git";
    private const string REPO_SSH = "git@github.com:credfeto/scratch.git";

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

    [Fact(Skip = "Requires SSH to be setup")]
    public Task CanCloneSshAsync()
    {
        return this.CloneTestCommonAsync(uri: REPO_SSH, cancellationToken: CancellationToken.None);
    }

    [Fact(Skip = "Requires SSH to be setup")]
    public async Task CreateBranchSshAsync()
    {
        CancellationToken cancellationToken = CancellationToken.None;

        using (Repository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: REPO_SSH, cancellationToken: cancellationToken))
        {
            this.Output.WriteLine($"Repo: {repo.Info.Path}");

            string newBranch = $"delete-me/{Guid.NewGuid()}".Replace(oldValue: "-", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
                                                            .ToLowerInvariant();

            if (GitUtils.DoesBranchExist(repo: repo, branchName: newBranch))
            {
                GitUtils.RemoveAllLocalBranches(repo);
            }

            GitUtils.CreateBranch(repo: repo, branchName: newBranch);

            Assert.True(GitUtils.DoesBranchExist(repo: repo, branchName: newBranch), userMessage: "Branch should exist");

            await GitUtils.ResetToMasterAsync(repo: repo, upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

            GitUtils.RemoveAllLocalBranches(repo);

            Assert.False(GitUtils.DoesBranchExist(repo: repo, branchName: newBranch), userMessage: "Branch should not exist");
        }
    }

    [Fact]
    public Task CanCloneHttpsAsync()
    {
        return this.CloneTestCommonAsync(uri: REPO_HTTPS, cancellationToken: CancellationToken.None);
    }

    private async Task CloneTestCommonAsync(string uri, CancellationToken cancellationToken)
    {
        using (Repository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken: cancellationToken))
        {
            this.Output.WriteLine($"Repo: {repo.Info.Path}");

            string branch = GitUtils.GetDefaultBranch(repo: repo, upstream: GitConstants.Upstream);
            this.Output.WriteLine($"Default Branch: {branch}");

            IReadOnlyCollection<string> remoteBranches = GitUtils.GetRemoteBranches(repo: repo, upstream: GitConstants.Upstream);

            foreach (string remoteBranch in remoteBranches)
            {
                this.Output.WriteLine($"* Branch: {remoteBranch}");
            }
        }

        using (Repository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken: cancellationToken))
        {
            GitUtils.RemoveAllLocalBranches(repo);
        }
    }
}