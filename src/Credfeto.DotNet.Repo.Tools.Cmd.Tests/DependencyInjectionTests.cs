using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.Cmd.Setup;
using Credfeto.DotNet.Repo.Tools.Dependencies;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void GitRepositoryListLoaderMustBeRegistered()
    {
        this.RequireService<IGitRepositoryListLoader>();
    }

    [Fact]
    public void BulkCodeCleanUpMustBeRegistered()
    {
        this.RequireService<IBulkCodeCleanUp>();
    }

    [Fact]
    public void BulkPackageUpdaterMustBeRegistered()
    {
        this.RequireService<IBulkPackageUpdater>();
    }

    [Fact]
    public void BulkTemplateUpdaterMustBeRegistered()
    {
        this.RequireService<IBulkTemplateUpdater>();
    }

    [Fact]
    public void BulkDependencyReducerMustBeRegistered()
    {
        this.RequireService<IBulkDependencyReducer>();
    }

    [Fact]
    public void DependencyReducerMustBeRegistered()
    {
        this.RequireService<IDependencyReducer>();
    }

    [Fact]
    public void DotNetFilesDetectorMustBeRegistered()
    {
        this.RequireService<IDotNetFilesDetector>();
    }

    [Fact]
    public void GitRepositoryFactoryMustBeRegistered()
    {
        this.RequireService<IGitRepositoryFactory>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddServices();
    }
}
