using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces.Tests;

public sealed class IBulkTemplateUpdaterTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(IBulkTemplateUpdater).IsInterface,
            userMessage: $"{nameof(IBulkTemplateUpdater)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(IBulkTemplateUpdater).IsPublic,
            userMessage: $"{nameof(IBulkTemplateUpdater)} must be public"
        );
    }

    [Fact]
    public void MustHaveBulkUpdateAsyncMethod()
    {
        Assert.NotNull(typeof(IBulkTemplateUpdater).GetMethod(nameof(IBulkTemplateUpdater.BulkUpdateAsync)));
    }
}
