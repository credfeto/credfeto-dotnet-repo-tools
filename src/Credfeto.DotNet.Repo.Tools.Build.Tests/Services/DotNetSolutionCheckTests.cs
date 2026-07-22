using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Services;

public sealed class DotNetSolutionCheckTests : TestBase
{
    private static readonly DotNetVersionSettings DotNetSettings = new(
        SdkVersion: "10.0.100",
        AllowPreRelease: false,
        RollForward: "latestPatch"
    );

    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;

    public DotNetSolutionCheckTests()
    {
        this._dotNetSolutionCheck = new DotNetSolutionCheck(
            GetSubstitute<IServiceProviderFactory>(),
            this.GetTypedLogger<DotNetSolutionCheck>()
        );
    }

    [Fact]
    public async Task PreCheckAsyncWithNoSolutionsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await this._dotNetSolutionCheck.PreCheckAsync(
            [],
            repositoryDotNetSettings: DotNetSettings,
            templateDotNetSettings: DotNetSettings,
            cancellationToken: cancellationToken
        );
    }

    [Fact]
    public async Task ReleaseCheckAsyncWithNoSolutionsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await this._dotNetSolutionCheck.ReleaseCheckAsync(
            [],
            repositoryDotNetSettings: DotNetSettings,
            cancellationToken: cancellationToken
        );
    }

    [Fact]
    public async Task PostCheckAsyncWithNoSolutionsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        bool result = await this._dotNetSolutionCheck.PostCheckAsync(
            [],
            repositoryDotNetSettings: DotNetSettings,
            templateDotNetSettings: DotNetSettings,
            cancellationToken: cancellationToken
        );

        Assert.True(condition: result, userMessage: "Empty solutions list should not report any errors");
    }
}
