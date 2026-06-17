using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces.Tests;

public sealed class IGlobalJsonTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(typeof(IGlobalJson).IsInterface, userMessage: $"{nameof(IGlobalJson)} must be an interface");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(IGlobalJson).IsPublic, userMessage: $"{nameof(IGlobalJson)} must be public");
    }

    [Fact]
    public void MustHaveLoadGlobalJsonAsyncMethod()
    {
        Assert.NotNull(typeof(IGlobalJson).GetMethod(nameof(IGlobalJson.LoadGlobalJsonAsync)));
    }
}
