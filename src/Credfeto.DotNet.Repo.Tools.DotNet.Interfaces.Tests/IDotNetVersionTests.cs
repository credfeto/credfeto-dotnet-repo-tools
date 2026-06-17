using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces.Tests;

public sealed class IDotNetVersionTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(typeof(IDotNetVersion).IsInterface, userMessage: $"{nameof(IDotNetVersion)} must be an interface");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(IDotNetVersion).IsPublic, userMessage: $"{nameof(IDotNetVersion)} must be public");
    }

    [Fact]
    public void MustHaveGetInstalledSdksAsyncMethod()
    {
        Assert.NotNull(typeof(IDotNetVersion).GetMethod(nameof(IDotNetVersion.GetInstalledSdksAsync)));
    }
}
