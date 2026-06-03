using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tracking.Interfaces.Tests;

public sealed class ITrackingHashGeneratorTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(
            typeof(ITrackingHashGenerator).IsInterface,
            userMessage: $"{nameof(ITrackingHashGenerator)} must be an interface"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(ITrackingHashGenerator).IsPublic,
            userMessage: $"{nameof(ITrackingHashGenerator)} must be public"
        );
    }

    [Fact]
    public void MustHaveGenerateTrackingHashAsyncMethod()
    {
        Assert.NotNull(
            typeof(ITrackingHashGenerator).GetMethod(nameof(ITrackingHashGenerator.GenerateTrackingHashAsync))
        );
    }
}
