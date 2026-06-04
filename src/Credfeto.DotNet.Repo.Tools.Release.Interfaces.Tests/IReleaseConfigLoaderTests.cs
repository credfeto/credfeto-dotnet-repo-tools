using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces.Tests;

public sealed class IReleaseConfigLoaderTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IReleaseConfigLoader).IsInterface,
            userMessage: $"{nameof(IReleaseConfigLoader)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(IReleaseConfigLoader).IsPublic,
            userMessage: $"{nameof(IReleaseConfigLoader)} must be public"
        );
    }

    [Fact]
    public void MustHaveLoadAsyncMethod()
    {
        Assert.NotNull(typeof(IReleaseConfigLoader).GetMethod(nameof(IReleaseConfigLoader.LoadAsync)));
    }
}
