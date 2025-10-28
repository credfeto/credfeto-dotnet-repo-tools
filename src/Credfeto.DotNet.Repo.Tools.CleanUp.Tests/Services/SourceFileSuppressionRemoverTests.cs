using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class SourceFileSuppressionRemoverTests : IntegrationTestBase
{
    private readonly ISourceFileSuppressionRemover _sourceFileSuppressionRemover;

    public SourceFileSuppressionRemoverTests(ITestOutputHelper output)
        : base(output)
    {
        this._sourceFileSuppressionRemover = new SourceFileSuppressionRemover();
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
        string actual = await this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(fileName: "example.txt", content: source, this.CancellationToken());

        Assert.Equal(expected: source, actual: actual);
    }

    [Fact]
    public async Task OneSuppressionShouldBeRemovedAsync()
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

        string actual = await this._sourceFileSuppressionRemover.RemoveSuppressionsAsync(fileName: "example.txt", content: source, this.CancellationToken());

        Assert.Equal(expected: expected, actual: actual);
    }
}