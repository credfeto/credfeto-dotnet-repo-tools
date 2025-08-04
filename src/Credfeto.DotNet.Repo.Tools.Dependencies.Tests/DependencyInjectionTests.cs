using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<ITrackingCache>()
                       .AddMockedService<IGlobalJson>()
                       .AddMockedService<IDotNetVersion>()
                       .AddMockedService<IDotNetBuild>()
                       .AddMockedService<IGitRepositoryFactory>()
                       .AddMockedService<IProjectFinder>()
                       .AddDependenciesReduction();
    }

    [Fact]
    public void BulkDependencyReducerIsRequired()
    {
        this.RequireService<IBulkDependencyReducer>();
    }

    [Fact]
    public void DependencyReducerIsRequired()
    {
        this.RequireService<IDependencyReducer>();
    }
}