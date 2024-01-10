using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Tests;

public sealed class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddDotNet();
    }

    [Fact]
    public void DotNetVersionMustBeRegistered()
    {
        this.RequireService<IDotNetVersion>();
    }

    [Fact]
    public void GlobalJsonMustBeRegistered()
    {
        this.RequireService<IGlobalJson>();
    }
}