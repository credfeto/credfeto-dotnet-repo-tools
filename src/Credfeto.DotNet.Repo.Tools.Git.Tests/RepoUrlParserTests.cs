using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class RepoUrlParserTests : LoggingTestBase
{
    public RepoUrlParserTests(ITestOutputHelper output)
        : base(output) { }

    private void ExpectedProtocol(
        GitUrlProtocol expectedProtocol,
        GitUrlProtocol protocol,
        string expectedHost,
        string? host,
        string expectedRepo,
        string? repo
    )
    {
        this.Output.WriteLine($"Protocol: {protocol.GetName()}");
        this.Output.WriteLine($"Host: {host}");
        this.Output.WriteLine($"Repo: {repo}");
        Assert.Equal(expected: expectedProtocol, actual: protocol);
        Assert.Equal(expected: expectedHost, actual: host);
        Assert.Equal(expected: expectedRepo, actual: repo);
    }

    [Theory]
    [InlineData(
        "https://github.com/meziantou/Meziantou.Analyzer.git",
        "github.com",
        "meziantou/Meziantou.Analyzer"
    )]
    [InlineData("https://gitlab.com/dwt1/wallpapers.git", "gitlab.com", "dwt1/wallpapers")]
    public void ShouldBeHttp(string path, string expectedHost, string expectedRepo)
    {
        bool ok = RepoUrlParser.TryParse(
            path: path,
            out GitUrlProtocol protocol,
            out string? host,
            out string? repo
        );
        Assert.True(condition: ok, userMessage: "Should have parsed");

        this.ExpectedProtocol(
            expectedProtocol: GitUrlProtocol.HTTP,
            protocol: protocol,
            expectedHost: expectedHost,
            host: host,
            expectedRepo: expectedRepo,
            repo: repo
        );
    }

    [Theory]
    [InlineData(
        "git@github.com:meziantou/Meziantou.Analyzer.git",
        "github.com",
        "meziantou/Meziantou.Analyzer"
    )]
    [InlineData("git@gitlab.com:dwt1/wallpapers.git", "gitlab.com", "dwt1/wallpapers")]
    public void ShouldBeSsh(string path, string expectedHost, string expectedRepo)
    {
        bool ok = RepoUrlParser.TryParse(
            path: path,
            out GitUrlProtocol protocol,
            out string? host,
            out string? repo
        );
        Assert.True(condition: ok, userMessage: "Should have parsed");
        this.ExpectedProtocol(
            expectedProtocol: GitUrlProtocol.SSH,
            protocol: protocol,
            expectedHost: expectedHost,
            host: host,
            expectedRepo: expectedRepo,
            repo: repo
        );
    }
}
