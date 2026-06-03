using System;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tracking.Interfaces.Tests;

public sealed class ITrackingCacheTests : TestBase
{
    [Fact]
    public void MustBeAnInterface()
    {
        Assert.True(typeof(ITrackingCache).IsInterface, userMessage: $"{nameof(ITrackingCache)} must be an interface");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(ITrackingCache).IsPublic, userMessage: $"{nameof(ITrackingCache)} must be public");
    }

    [Fact]
    public void MustHaveLoadAsyncMethod()
    {
        Assert.NotNull(typeof(ITrackingCache).GetMethod(nameof(ITrackingCache.LoadAsync)));
    }

    [Fact]
    public void MustHaveSaveAsyncMethod()
    {
        Assert.NotNull(typeof(ITrackingCache).GetMethod(nameof(ITrackingCache.SaveAsync)));
    }

    [Fact]
    public void MustHaveGetMethod()
    {
        Assert.NotNull(typeof(ITrackingCache).GetMethod(nameof(ITrackingCache.Get)));
    }

    [Fact]
    public void MustHaveSetMethod()
    {
        Assert.NotNull(typeof(ITrackingCache).GetMethod(nameof(ITrackingCache.Set)));
    }
}
