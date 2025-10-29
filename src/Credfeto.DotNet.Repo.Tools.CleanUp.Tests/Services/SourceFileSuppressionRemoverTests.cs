using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class SourceFileSuppressionRemoverTests : IntegrationTestBase
{
    private readonly BuildContext _buildContext;
    private readonly IDotNetBuild _dotNetBuild;
    private readonly ISourceFileSuppressionRemover _sourceFileSuppressionRemover;

    public SourceFileSuppressionRemoverTests(ITestOutputHelper output)
        : base(output)
    {
        this._buildContext = new(SourceDirectory: "/test", new([], [], Framework: null), new(PreRelease: true));
        this._dotNetBuild = GetSubstitute<IDotNetBuild>();
        this._sourceFileSuppressionRemover = new SourceFileSuppressionRemover(dotNetBuild: this._dotNetBuild, this.GetTypedLogger<SourceFileSuppressionRemover>());
    }

    [Fact]
    public async Task FileWithNoSuppressionsShouldNotBeChangedAsync()
    {
        const string source = @"
using System.Diagnostics;

namespace Test;

public static class Test {

    public static void DoesNothing() {
          // Example
    }
}
";

        string actual = await this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(fileName: "example.txt", content: source, buildContext: this._buildContext, this.CancellationToken());

        Assert.Equal(expected: source, actual: actual);

        await this.DidNotReceiveBuildAsync();
    }

    private ValueTask ReceivedBuildAsync(int times)
    {
        return this._dotNetBuild.Received(times)
                   .BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    private ValueTask DidNotReceiveBuildAsync()
    {
        return this._dotNetBuild.DidNotReceive()
                   .BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OneSuppressionShouldBeRemovedIfBuildSucceedsAsync()
    {
        const string source = @"
using System.Diagnostics;

namespace Test;

public static class Test {

    [SuppressMessage(category: ""Meziantou.Analyzer"", checkId: ""MA0051: Method is too long"", Justification = ""Unit tests"")]
    public static void DoesNothing() {
          // Example
    }
}
";

        const string expected = @"
using System.Diagnostics;

namespace Test;

public static class Test {

    public static void DoesNothing() {
          // Example
    }
}
";

        string actual = await this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(fileName: "example.txt", content: source, buildContext: this._buildContext, this.CancellationToken());

        Assert.Equal(expected: expected, actual: actual);

        await this.ReceivedBuildAsync(1);
    }
}