using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class GitRepositoryFactoryTests : LoggingFolderCleanupTestBase
{
    private readonly IGitRepositoryFactory _gitRepositoryFactory;

    public GitRepositoryFactoryTests(ITestOutputHelper output)
        : base(output)
    {
        IGitRepositoryLocator gitRepositoryLocator = GetSubstitute<IGitRepositoryLocator>();
        gitRepositoryLocator.GetWorkingDirectory(Arg.Any<string>(), Arg.Any<string>())
                            .Returns(Path.Combine(path1: this.TempFolder, path2: "scratch"));

        this._gitRepositoryFactory = new GitRepositoryFactory(locator: gitRepositoryLocator, this.GetTypedLogger<GitRepositoryFactory>());
    }

    [Fact(Skip = "Requires SSH to be setup")]
    public Task CanCloneSshAsync()
    {
        return this.CloneTestCommonAsync(uri: Repositories.GitHubSsh, cancellationToken: CancellationToken.None);
    }

    [Fact(Skip = "Requires SSH to be setup")]
    public async Task CreateBranchSshAsync()
    {
        CancellationToken cancellationToken = CancellationToken.None;

        using (IGitRepository repo =
               await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: Repositories.GitHubSsh, cancellationToken: cancellationToken))
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
        return this.CloneTestCommonAsync(uri: Repositories.GitHubHttps, cancellationToken: CancellationToken.None);
    }

    private async Task CloneTestCommonAsync(string uri, CancellationToken cancellationToken)
    {
        using (IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken: cancellationToken))
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

        using (IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: this.TempFolder, repoUrl: uri, cancellationToken: cancellationToken))
        {
            repo.RemoveAllLocalBranches();
        }
    }
}