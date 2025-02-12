using Credfeto.Date.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.BuildVersion.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddMockedService<ICurrentTimeSource>()
            .AddMockedService<IDotNetBuild>()
            .AddMockedService<IDotNetSolutionCheck>()
            .AddMockedService<ITrackingCache>()
            .AddMockedService<IVersionDetector>()
            .AddReleaseGeneration();
    }

    [Fact]
    public void ReleaseGenerationMustBeRegistered()
    {
        this.RequireService<IReleaseGeneration>();
    }

    [Fact]
    public void ReleaseConfigLoaderMustBeRegistered()
    {
        this.RequireService<IReleaseConfigLoader>();
    }
}
