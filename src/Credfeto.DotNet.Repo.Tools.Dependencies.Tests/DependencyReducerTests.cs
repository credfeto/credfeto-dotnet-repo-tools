using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using FunFair.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Tests;

public sealed class DependencyReducerTests : LoggingTestBase
{
    public DependencyReducerTests(ITestOutputHelper output)
        : base(output: output, dependencyInjectionRegistration: Configure)
    {
    }

    private static IServiceCollection Configure(IServiceCollection services)
    {
        return services.AddMockedService<ITrackingCache>()
                       .AddBuild()
                       .AddDependenciesReduction();
    }

    [Fact]
    public async Task ReduceAsync()
    {
        const string sourceDirectory = "/home/markr/work/personal/credfeto-date/src";
        ReferenceConfig referenceConfig = new();

        IDependencyReducer dependencyReducer = this.GetServiceFromDependencyInjection<IDependencyReducer>();

        await dependencyReducer.CheckReferencesAsync(sourceDirectory: sourceDirectory, config: referenceConfig, this.CancellationToken());

        this.Output.WriteLine("Completed");
    }
}