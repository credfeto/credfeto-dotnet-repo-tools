using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests;

public sealed class IGitRepositoryListLoaderTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IGitRepositoryListLoader).IsInterface,
            userMessage: $"{nameof(IGitRepositoryListLoader)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(IGitRepositoryListLoader).IsPublic,
            userMessage: $"{nameof(IGitRepositoryListLoader)} must be public"
        );
    }

    [Fact]
    public void MustHaveLoadAsyncMethod()
    {
        Assert.NotNull(typeof(IGitRepositoryListLoader).GetMethod(nameof(IGitRepositoryListLoader.LoadAsync)));
    }
}
