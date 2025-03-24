using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure) { }

    [Fact]
    public void GitRepositoryLocatorMustBeRegistered()
    {
        this.RequireService<IGitRepositoryLocator>();
    }

    [Fact]
    public void GitRepositoryFactoryMustBeRegistered()
    {
        this.RequireService<IGitRepositoryFactory>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddGit();
    }
}
