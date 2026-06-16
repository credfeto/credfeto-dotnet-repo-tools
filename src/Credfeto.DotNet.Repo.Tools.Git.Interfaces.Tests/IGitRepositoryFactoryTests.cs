using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests;

public sealed class IGitRepositoryFactoryTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IGitRepositoryFactory).IsInterface,
            userMessage: $"{nameof(IGitRepositoryFactory)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(IGitRepositoryFactory).IsPublic,
            userMessage: $"{nameof(IGitRepositoryFactory)} must be public"
        );
    }

    [Fact]
    public void MustHaveOpenOrCloneAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepositoryFactory).GetMethod(nameof(IGitRepositoryFactory.OpenOrCloneAsync)));
    }
}
