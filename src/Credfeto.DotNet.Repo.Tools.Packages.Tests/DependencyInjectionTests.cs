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

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void BulkPackageUpdaterMustBeRegistered()
    {
        this.RequireService<IBulkPackageUpdater>();
    }

    [Fact]
    public void BulkPackageConfigLoaderMustBeRegistered()
    {
        this.RequireService<IBulkPackageConfigLoader>();
    }

    [Fact]
    public void SinglePackageUpdaterMustBeRegistered()
    {
        this.RequireService<ISinglePackageUpdater>();
    }

    [Fact]
    public void PackageUpdateConfigurationBuilderMustBeRegistered()
    {
        this.RequireService<IPackageUpdateConfigurationBuilder>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services
            .AddMockedService<IDotNetBuild>()
            .AddMockedService<IDotNetSolutionCheck>()
            .AddMockedService<IDotNetFilesDetector>()
            .AddMockedService<IGitRepositoryFactory>()
            .AddMockedService<IGlobalJson>()
            .AddMockedService<IDotNetVersion>()
            .AddMockedService<IPackageUpdater>()
            .AddMockedService<IPackageCache>()
            .AddMockedService<IReleaseConfigLoader>()
            .AddMockedService<IReleaseGeneration>()
            .AddMockedService<ITrackingCache>()
            .AddMockedService<ITrackingHashGenerator>()
            .AddBulkPackageUpdater();
    }
}
