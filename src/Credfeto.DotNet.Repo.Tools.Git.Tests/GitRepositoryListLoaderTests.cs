using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Git.Tests;

public sealed class GitRepositoryListLoaderTests : LoggingFolderCleanupTestBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitRepositoryListLoader _loader;

    public GitRepositoryListLoaderTests(ITestOutputHelper output)
        : base(output)
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();
        this._loader = new GitRepositoryListLoader(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<GitRepositoryListLoader>()
        );
    }

    [Fact]
    public async Task LoadAsync_FromFile_ReturnsRepos()
    {
        string filePath = Path.Combine(this.TempFolder, "repos.txt");
        await File.WriteAllTextAsync(
            path: filePath,
            contents: "https://github.com/org/repo1\nhttps://github.com/org/repo2\n",
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: filePath,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "https://github.com/org/repo1", actual: result[0]);
        Assert.Equal(expected: "https://github.com/org/repo2", actual: result[1]);
    }

    [Fact]
    public async Task LoadAsync_FromFile_FiltersEmptyLines()
    {
        string filePath = Path.Combine(this.TempFolder, "repos-empty.txt");
        await File.WriteAllTextAsync(
            path: filePath,
            contents: "https://github.com/org/repo1\n\n\nhttps://github.com/org/repo2\n",
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: filePath,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task LoadAsync_FromFile_FiltersWhitespaceOnlyLines()
    {
        string filePath = Path.Combine(this.TempFolder, "repos-whitespace.txt");
        await File.WriteAllTextAsync(
            path: filePath,
            contents: "https://github.com/org/repo1\n   \n\t\nhttps://github.com/org/repo2\n",
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: filePath,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task LoadAsync_FromFile_TrimsLeadingAndTrailingWhitespace()
    {
        string filePath = Path.Combine(this.TempFolder, "repos-padded.txt");
        await File.WriteAllTextAsync(
            path: filePath,
            contents: "  https://github.com/org/repo1  \n  https://github.com/org/repo2  \n",
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: filePath,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "https://github.com/org/repo1", actual: result[0]);
        Assert.Equal(expected: "https://github.com/org/repo2", actual: result[1]);
    }

    [Fact]
    public async Task LoadAsync_FromHttps_ReturnsRepos()
    {
        const string content = "https://github.com/org/repo1\nhttps://github.com/org/repo2\n";

        using FakeHttpMessageHandler handler = new(content);
        using HttpClient client = new(handler, disposeHandler: false);
        this._httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: "https://example.com/repos.txt",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "https://github.com/org/repo1", actual: result[0]);
        Assert.Equal(expected: "https://github.com/org/repo2", actual: result[1]);
    }

    [Fact]
    public async Task LoadAsync_FromHttp_ReturnsRepos()
    {
        const string content = "https://github.com/org/repo1\nhttps://github.com/org/repo2\n";

        using FakeHttpMessageHandler handler = new(content);
        using HttpClient client = new(handler, disposeHandler: false);
        this._httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: "http://example.com/repos.txt",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task LoadAsync_FromHttps_WithWindowsLineEndings_ReturnsRepos()
    {
        const string content = "https://github.com/org/repo1\r\nhttps://github.com/org/repo2\r\n";

        using FakeHttpMessageHandler handler = new(content);
        using HttpClient client = new(handler, disposeHandler: false);
        this._httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: "https://example.com/repos.txt",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
        Assert.Equal(expected: "https://github.com/org/repo1", actual: result[0]);
        Assert.Equal(expected: "https://github.com/org/repo2", actual: result[1]);
    }

    [Fact]
    public async Task LoadAsync_FromHttps_WithMixedLineEndings_ReturnsRepos()
    {
        const string content =
            "https://github.com/org/repo1\nhttps://github.com/org/repo2\r\nhttps://github.com/org/repo3\r";

        using FakeHttpMessageHandler handler = new(content);
        using HttpClient client = new(handler, disposeHandler: false);
        this._httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: "https://example.com/repos.txt",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 3, actual: result.Count);
    }

    [Fact]
    public async Task LoadAsync_FromHttps_FiltersEmptyLines()
    {
        const string content = "https://github.com/org/repo1\n\nhttps://github.com/org/repo2\n";

        using FakeHttpMessageHandler handler = new(content);
        using HttpClient client = new(handler, disposeHandler: false);
        this._httpClientFactory.CreateClient(Arg.Any<string>()).Returns(client);

        IReadOnlyList<string> result = await this._loader.LoadAsync(
            path: "https://example.com/repos.txt",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public FakeHttpMessageHandler(string responseContent)
        {
            this._responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(this._responseContent),
            };

            return Task.FromResult(response);
        }
    }
}
