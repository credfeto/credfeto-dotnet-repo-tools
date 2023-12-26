using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

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
        string actual = GitUtils.GetWorkingDirectoryForRepository(gitUrl);

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

        using (IGitRepository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: REPO_SSH, cancellationToken: cancellationToken))
        {
            this.Output.WriteLine($"Repo: {repo.ClonePath}");

            string newBranch = $"delete-me/{Guid.NewGuid()}".Replace(oldValue: "-", newValue: string.Empty, comparisonType: StringComparison.Ordinal)
                                                            .ToLowerInvariant();

            if (repo.DoesBranchExist(branchName: newBranch))
            {
                repo.RemoveAllLocalBranches();
            }

            await repo.CreateBranchAsync(branchName: newBranch, cancellationToken: cancellationToken);

            Assert.True(repo.DoesBranchExist(branchName: newBranch), userMessage: "Branch should exist");

            await repo.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

            repo.RemoveAllLocalBranches();

            Assert.False(repo.DoesBranchExist(branchName: newBranch), userMessage: "Branch should not exist");
        }
    }

    [Fact]
    public Task CanCloneHttpsAsync()
    {
        return this.CloneTestCommonAsync(uri: REPO_HTTPS, cancellationToken: CancellationToken.None);
    }

    private async Task CloneTestCommonAsync(string uri, CancellationToken cancellationToken)
    {
        using (IGitRepository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken: cancellationToken))
        {
            this.Output.WriteLine($"Repo: {repo.ClonePath}");

            string branch = repo.GetDefaultBranch(upstream: GitConstants.Upstream);
            this.Output.WriteLine($"Default Branch: {branch}");

            IReadOnlyCollection<string> remoteBranches = repo.GetRemoteBranches(upstream: GitConstants.Upstream);

            foreach (string remoteBranch in remoteBranches)
            {
                this.Output.WriteLine($"* Branch: {remoteBranch}");
            }
        }

        using (IGitRepository repo = await GitUtils.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken: cancellationToken))
        {
            repo.RemoveAllLocalBranches();
        }
    }
}