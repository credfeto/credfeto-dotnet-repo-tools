using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    [Fact]
    public void DotNetBuildMustBeRegistered()
    {
        this.RequireService<IDotNetBuild>();
    }

    [Fact]
    public void SolutionCheckMustBeRegistered()
    {
        this.RequireService<IDotNetSolutionCheck>();
    }

    [Fact]
    public void DotNetFilesDetectorMustBeRegistered()
    {
        this.RequireService<IDotNetFilesDetector>();
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<IServiceProviderFactory>()
                       .AddBuild();
    }
}