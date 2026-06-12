using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Interfaces.Tests;

public sealed class IBulkPackageConfigLoaderTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IBulkPackageConfigLoader).IsInterface,
            userMessage: $"{nameof(IBulkPackageConfigLoader)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(IBulkPackageConfigLoader).IsPublic,
            userMessage: $"{nameof(IBulkPackageConfigLoader)} must be public"
        );
    }

    [Fact]
    public void MustHaveLoadAsyncMethod()
    {
        Assert.NotNull(typeof(IBulkPackageConfigLoader).GetMethod(nameof(IBulkPackageConfigLoader.LoadAsync)));
    }
}
