using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces.Tests;

public sealed class GitConstantsTests : TestBase
{
    [Fact]
    public void MustBeStaticClass()
    {
        Assert.True(
            typeof(GitConstants).IsAbstract && typeof(GitConstants).IsSealed,
            userMessage: $"{nameof(GitConstants)} must be a static class"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(GitConstants).IsPublic, userMessage: $"{nameof(GitConstants)} must be public");
    }

    [Fact]
    public void UpstreamMustBeOrigin()
    {
        Assert.Equal(expected: "origin", actual: GitConstants.Upstream);
    }
}
