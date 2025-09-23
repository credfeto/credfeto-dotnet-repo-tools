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
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    [Fact]
    public void BulkTemplateUpdaterMustBeRegistered()
    {
        this.RequireService<IBulkTemplateUpdater>();
    }

    [Fact]
    public void TemplateConfigLoaderMustBeRegistered()
    {
        this.RequireService<ITemplateConfigLoader>();
    }

    [Fact]
    public void FileUpdaterLoaderMustBeRegistered()
    {
        this.RequireService<IFileUpdater>();
    }

    [Fact]
    public void LabelsBuilderMustBeRegistered()
    {
        this.RequireService<ILabelsBuilder>();
    }

    [Fact]
    public void DependaBotConfigBuilderMustBeRegistered()
    {
        this.RequireService<IDependaBotConfigBuilder>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<IBulkPackageConfigLoader>()
                       .AddMockedService<IDotNetBuild>()
                       .AddMockedService<IDotNetSolutionCheck>()
                       .AddMockedService<IDotNetVersion>()
                       .AddMockedService<IGitRepositoryFactory>()
                       .AddMockedService<IGlobalJson>()
                       .AddMockedService<IReleaseConfigLoader>()
                       .AddMockedService<IReleaseGeneration>()
                       .AddMockedService<ITrackingCache>()
                       .AddMockedService<IDotNetFilesDetector>()
                       .AddTemplateUpdate();
    }
}