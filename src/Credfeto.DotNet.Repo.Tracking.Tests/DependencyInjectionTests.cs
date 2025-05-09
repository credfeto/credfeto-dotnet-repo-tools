﻿using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tracking.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void TrackingCacheMustBeRegistered()
    {
        this.RequireService<ITrackingCache>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddTracking();
    }
}
