using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Integration.Tests;

public sealed class GitRepositoryFactoryIntegrationTests : LoggingFolderCleanupTestBase
{
    private readonly IGitRepositoryFactory _gitRepositoryFactory;

    public GitRepositoryFactoryIntegrationTests(ITestOutputHelper output)
        : base(output)
    {
        IGitRepositoryLocator gitRepositoryLocator = GetSubstitute<IGitRepositoryLocator>();
        gitRepositoryLocator
            .GetWorkingDirectory(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Path.Combine(path1: this.TempFolder, path2: "scratch"));

        this._gitRepositoryFactory = new GitRepositoryFactory(
            locator: gitRepositoryLocator,
            this.GetTypedLogger<GitRepositoryFactory>()
        );
    }

    [Fact(Skip = "Requires network access to GitHub")]
    public Task CanCloneHttpsAsync()
    {
        return this.CloneTestCommonAsync(uri: Repositories.GitHubHttps, cancellationToken: this.CancellationToken());
    }

    private async Task CloneTestCommonAsync(string uri, CancellationToken cancellationToken)
    {
        using (
            IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: uri,
                cancellationToken: cancellationToken
            )
        )
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

        using (
            IGitRepository repo = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: this.TempFolder,
                repoUrl: uri,
                cancellationToken: cancellationToken
            )
        )
        {
            repo.RemoveAllLocalBranches();
        }
    }
}
