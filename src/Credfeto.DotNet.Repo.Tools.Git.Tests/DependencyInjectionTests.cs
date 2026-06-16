using System.Net.Http;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Services;
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

    [Fact]
    public void GitRepositoryListLoaderMustBeRegistered()
    {
        this.RequireService<IGitRepositoryListLoader>();
    }

    [Fact]
    public void GitRepositoryListLoaderHttpClientCanBeCreated()
    {
        IHttpClientFactory httpClientFactory = this.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        using HttpClient client = httpClientFactory.CreateClient(nameof(GitRepositoryListLoader));

        Assert.NotNull(client);
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddGit();
    }
}
