using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Package;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    [Fact]
    public void BulkPackageUpdaterMustBeRegistered()
    {
        this.RequireService<IBulkPackageUpdater>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<IDotNetBuild>()
                       .AddMockedService<IDotNetSolutionCheck>()
                       .AddMockedService<IGitRepositoryFactory>()
                       .AddMockedService<IGlobalJson>()
                       .AddMockedService<IPackageUpdater>()
                       .AddMockedService<IPackageCache>()
                       .AddMockedService<IReleaseGeneration>()
                       .AddMockedService<ITrackingCache>()
                       .AddBulkPackageUpdater();
    }
}