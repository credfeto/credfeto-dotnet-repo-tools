using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    [Fact]
    public void CleanUpBuildMustBeRegistered()
    {
        this.RequireService<IBulkCodeCleanUp>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<ITrackingCache>()
                       .AddMockedService<IGitRepositoryFactory>()
                       .AddMockedService<IGlobalJson>()
                       .AddMockedService<IProjectXmlRewriter>()
                       .AddMockedService<IReleaseConfigLoader>()
                       .AddMockedService<IDotNetVersion>()
                       .AddCleanUp();
    }
}