using System;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Cmd.Services;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Tests.Services;

public sealed class ServiceProviderFactoryTests : TestBase
{
    private readonly IServiceProviderFactory _serviceProviderFactory;

    public ServiceProviderFactoryTests()
    {
        this._serviceProviderFactory = new ServiceProviderFactory();
    }

    [Fact]
    public async Task BuildResolvesRegisteredServicesAsync()
    {
        IGitRepositoryListLoader gitRepositoryListLoader = GetSubstitute<IGitRepositoryListLoader>();
        IServiceCollection services = new ServiceCollection().AddSingleton(gitRepositoryListLoader);

        IServiceProvider serviceProvider = this._serviceProviderFactory.Build(services);

        await using (ServiceProvider built = Assert.IsType<ServiceProvider>(serviceProvider))
        {
            Assert.Same(
                expected: gitRepositoryListLoader,
                actual: built.GetRequiredService<IGitRepositoryListLoader>()
            );
        }
    }
}
