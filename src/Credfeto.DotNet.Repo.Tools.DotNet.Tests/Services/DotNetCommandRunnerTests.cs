using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Tests.Services;

public sealed class DotNetCommandRunnerTests : LoggingTestBase
{
    private readonly IDotNetCommandRunner _commandRunner;

    public DotNetCommandRunnerTests(ITestOutputHelper output)
        : base(output)
    {
        this._commandRunner = new DotNetCommandRunner();
    }

    [Fact]
    public async Task RunListSdksMustSucceedAsync()
    {
        (string[] output, int exitCode) = await this._commandRunner.RunAsync(
            arguments: "--list-sdks",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 0, actual: exitCode);
        Assert.NotEmpty(output);
    }
}
