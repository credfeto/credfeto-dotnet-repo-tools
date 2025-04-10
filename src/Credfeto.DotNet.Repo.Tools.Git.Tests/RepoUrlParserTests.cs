using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class RepoUrlParserTests : LoggingTestBase
{
    public RepoUrlParserTests(ITestOutputHelper output)
        : base(output) { }

    private void ExpectedProtocol(
        string expectedHost,
        string? host,
        GitUrlProtocol expectedProtocol,
        GitUrlProtocol protocol
    )
    {
        this.Output.WriteLine($"Protocol: {protocol.GetName()}");
        this.Output.WriteLine($"Host: {host}");
        Assert.Equal(expected: expectedProtocol, actual: protocol);
        Assert.Equal(expected: expectedHost, actual: host);
    }

    [Theory]
    [InlineData("https://github.com/meziantou/Meziantou.Analyzer.git", "github.com")]
    [InlineData("https://gitlab.com/dwt1/wallpapers.git", "gitlab.com")]
    public void ShouldBeHttp(string repo, string expectedHost)
    {
        bool ok = RepoUrlParser.TryParse(path: repo, out GitUrlProtocol protocol, out string? host);
        Assert.True(condition: ok, userMessage: "Should have parsed");

        this.ExpectedProtocol(
            expectedHost: expectedHost,
            host: host,
            expectedProtocol: GitUrlProtocol.HTTP,
            protocol: protocol
        );
    }

    [Theory]
    [InlineData("git@github.com:meziantou/Meziantou.Analyzer.git", "github.com")]
    [InlineData("git@gitlab.com:dwt1/wallpapers.git", "gitlab.com")]
    public void ShouldBeSsh(string repo, string expectedHost)
    {
        bool ok = RepoUrlParser.TryParse(path: repo, out GitUrlProtocol protocol, out string? host);
        Assert.True(condition: ok, userMessage: "Should have parsed");
        this.ExpectedProtocol(
            expectedHost: expectedHost,
            host: host,
            expectedProtocol: GitUrlProtocol.SSH,
            protocol: protocol
        );
    }
}
