using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void CleanUpBuildMustBeRegistered()
    {
        this.RequireService<IBulkTemplateUpdater>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddMockedService<IBulkPackageConfigLoader>()
            .AddMockedService<IDotNetBuild>()
            .AddMockedService<IDotNetSolutionCheck>()
            .AddMockedService<IDotNetVersion>()
            .AddMockedService<IGitRepositoryFactory>()
            .AddMockedService<IGlobalJson>()
            .AddMockedService<IReleaseConfigLoader>()
            .AddMockedService<IReleaseGeneration>()
            .AddMockedService<ITrackingCache>()
            .AddTemplateUpdate();
    }
}
