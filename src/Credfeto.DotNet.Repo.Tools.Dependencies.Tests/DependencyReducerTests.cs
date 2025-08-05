using System.IO;
using System.Threading;
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

    [Fact(Skip = "Enable manually to test")]
    public async Task ReduceAsync()
    {
        const string sourceDirectory = "/home/markr/work/personal/credfeto-date/src";

        if (!Directory.Exists(sourceDirectory))
        {
            Assert.Skip("Sourceproject does not exist ");

            return;
        }

        ReferenceConfig referenceConfig = new(this.CommitAsync);

        IDependencyReducer dependencyReducer = this.GetServiceFromDependencyInjection<IDependencyReducer>();

        await dependencyReducer.CheckReferencesAsync(sourceDirectory: sourceDirectory, config: referenceConfig, this.CancellationToken());

        this.Output.WriteLine("Completed");
    }

    private ValueTask CommitAsync(string projectFileName, string message, CancellationToken cancellationToken)
    {
        this.Output.WriteLine($"{projectFileName}: {message}");

        return ValueTask.CompletedTask;
    }
}