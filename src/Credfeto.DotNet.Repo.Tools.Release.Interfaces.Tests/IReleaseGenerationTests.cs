using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces.Tests;

public sealed class IReleaseGenerationTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IReleaseGeneration).IsInterface,
            userMessage: $"{nameof(IReleaseGeneration)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(IReleaseGeneration).IsPublic, userMessage: $"{nameof(IReleaseGeneration)} must be public");
    }

    [Fact]
    public void MustHaveTryCreateNextPatchAsyncMethod()
    {
        Assert.NotNull(typeof(IReleaseGeneration).GetMethod(nameof(IReleaseGeneration.TryCreateNextPatchAsync)));
    }

    [Fact]
    public void MustHaveCreateAsyncMethod()
    {
        Assert.NotNull(typeof(IReleaseGeneration).GetMethod(nameof(IReleaseGeneration.CreateAsync)));
    }
}
