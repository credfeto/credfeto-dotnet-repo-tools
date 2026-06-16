using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests;

public sealed class IGitRepositoryLocatorTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IGitRepositoryLocator).IsInterface,
            userMessage: $"{nameof(IGitRepositoryLocator)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(IGitRepositoryLocator).IsPublic,
            userMessage: $"{nameof(IGitRepositoryLocator)} must be public"
        );
    }

    [Fact]
    public void MustHaveGetWorkingDirectoryMethod()
    {
        Assert.NotNull(typeof(IGitRepositoryLocator).GetMethod(nameof(IGitRepositoryLocator.GetWorkingDirectory)));
    }
}
