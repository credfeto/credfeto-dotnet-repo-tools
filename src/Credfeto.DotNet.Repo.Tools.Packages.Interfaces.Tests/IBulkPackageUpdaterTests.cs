using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Interfaces.Tests;

public sealed class IBulkPackageUpdaterTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IBulkPackageUpdater).IsInterface,
            userMessage: $"{nameof(IBulkPackageUpdater)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(IBulkPackageUpdater).IsPublic, userMessage: $"{nameof(IBulkPackageUpdater)} must be public");
    }

    [Fact]
    public void MustHaveBulkUpdateAsyncMethod()
    {
        Assert.NotNull(typeof(IBulkPackageUpdater).GetMethod(nameof(IBulkPackageUpdater.BulkUpdateAsync)));
    }
}
