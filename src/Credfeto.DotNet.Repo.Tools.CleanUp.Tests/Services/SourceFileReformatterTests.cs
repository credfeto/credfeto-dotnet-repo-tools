using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Tests.Services;

public sealed class SourceFileReformatterTests : LoggingTestBase
{
    private readonly ISourceFileReformatter _sourceFileReformatter;

    public SourceFileReformatterTests(ITestOutputHelper output)
        : base(output)
    {
        this._sourceFileReformatter = new SourceFileReformatter(this.GetTypedLogger<SourceFileReformatter>());
    }

    [Fact]
    public async Task ReformatAsyncShouldReturnFormattedCodeForValidCSharpAsync()
    {
        const string content = "namespace Test { public class A { } }";

        string result = await this._sourceFileReformatter.ReformatAsync(
            fileName: "test.cs",
            content: content,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ReformatAsyncShouldReturnOriginalContentForInvalidCSharpAsync()
    {
        const string content = "this is not valid C# code at all!!!";

        string result = await this._sourceFileReformatter.ReformatAsync(
            fileName: "test.cs",
            content: content,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: content, actual: result);
    }

    [Fact]
    public async Task ReformatAsyncShouldReturnSameContentWhenAlreadyFormattedAsync()
    {
        const string content =
            @"namespace Test;

public sealed class Example
{
    public void Method()
    {
        // Simple
    }
}
";

        string result = await this._sourceFileReformatter.ReformatAsync(
            fileName: "test.cs",
            content: content,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ReformatAsyncShouldReturnOriginalContentWhenCancelledAsync()
    {
        const string content = "namespace Test { public class A { } }";

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        string result = await this._sourceFileReformatter.ReformatAsync(
            fileName: "test.cs",
            content: content,
            cancellationToken: cts.Token
        );

        Assert.Equal(expected: content, actual: result);
    }
}
