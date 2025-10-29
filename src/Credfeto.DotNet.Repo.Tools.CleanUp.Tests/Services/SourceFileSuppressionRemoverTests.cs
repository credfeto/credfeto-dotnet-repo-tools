using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class SourceFileSuppressionRemoverTests : LoggingFolderCleanupTestBase
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

        string actual = await this.CleanupAsync(source);

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

        this.MockSuccessfulBuild();

        string actual = await this.CleanupAsync(source);

        Assert.Equal(expected: expected, actual: actual);

        await this.ReceivedBuildAsync(1);
    }

    [SuppressMessage(category: "Microsoft.Design", checkId: "CA2012: Should be awaited", Justification = "Mocking not executing")]
    private void MockSuccessfulBuild()
    {
        _ = this._dotNetBuild.BuildAsync(Arg.Any<BuildContext>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.CompletedTask);
    }

    private async Task<string> CleanupAsync(string source)
    {
        string fileName = Path.Combine(path1: this.TempFolder, path2: "example.cs");

        string actual = await this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(fileName: fileName, content: source, buildContext: this._buildContext, this.CancellationToken());

        return actual;
    }
}