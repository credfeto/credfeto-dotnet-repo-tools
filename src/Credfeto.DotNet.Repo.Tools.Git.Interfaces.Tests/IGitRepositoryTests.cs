using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests;

public sealed class IGitRepositoryTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(typeof(IGitRepository).IsInterface, userMessage: $"{nameof(IGitRepository)} must be an interface");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(IGitRepository).IsPublic, userMessage: $"{nameof(IGitRepository)} must be public");
    }

    [Fact]
    public void MustHaveRemoveAllLocalBranchesMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.RemoveAllLocalBranches)));
    }

    [Fact]
    public void MustHaveResetToDefaultBranchAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.ResetToDefaultBranchAsync)));
    }

    [Fact]
    public void MustHaveGetDefaultBranchMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.GetDefaultBranch)));
    }

    [Fact]
    public void MustHaveHasUncommittedChangesMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.HasUncommittedChanges)));
    }

    [Fact]
    public void MustHaveGetRemoteBranchesMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.GetRemoteBranches)));
    }

    [Fact]
    public void MustHaveCommitAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.CommitAsync)));
    }

    [Fact]
    public void MustHaveCommitNamedAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.CommitNamedAsync)));
    }

    [Fact]
    public void MustHavePushAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.PushAsync)));
    }

    [Fact]
    public void MustHavePushOriginAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.PushOriginAsync)));
    }

    [Fact]
    public void MustHaveDoesBranchExistMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.DoesBranchExist)));
    }

    [Fact]
    public void MustHaveCreateBranchAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.CreateBranchAsync)));
    }

    [Fact]
    public void MustHaveRemoveBranchesForPrefixAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.RemoveBranchesForPrefixAsync)));
    }

    [Fact]
    public void MustHaveGetLastCommitDateMethod()
    {
        Assert.NotNull(typeof(IGitRepository).GetMethod(nameof(IGitRepository.GetLastCommitDate)));
    }
}
