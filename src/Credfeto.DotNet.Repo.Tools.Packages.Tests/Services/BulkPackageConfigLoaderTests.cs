using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Packages.Tests.Services;

public sealed class BulkPackageConfigLoaderTests : LoggingFolderCleanupTestBase
{
    private const string ValidPackageJson =
        """[{"packageId":"Test.Package","type":"nuget","exact-match":false,"version-bump-package":false,"prohibit-version-bump-when-referenced":false}]""";

    private const string EmptyPackageJson = "[]";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBulkPackageConfigLoader _loader;

    public BulkPackageConfigLoaderTests(ITestOutputHelper output)
        : base(output)
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();

        this._loader = new BulkPackageConfigLoader(
            httpClientFactory: this._httpClientFactory,
            logger: this.GetTypedLogger<BulkPackageConfigLoader>()
        );
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public MockHttpMessageHandler(string responseContent)
        {
            this._responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        content: this._responseContent,
                        encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        mediaType: "application/json"
                    ),
                }
            );
        }
    }

    [Fact]
    public async Task LoadFromFileWithValidContentReturnsPackagesAsync()
    {
        string fileName = Path.Combine(path1: this.TempFolder, path2: "packages.json");
        await File.WriteAllTextAsync(
            path: fileName,
            contents: ValidPackageJson,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<PackageUpdate> result = await this._loader.LoadAsync(
            path: fileName,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task LoadFromFileWithValidContentReturnsCorrectPackageIdAsync()
    {
        string fileName = Path.Combine(path1: this.TempFolder, path2: "packages.json");
        await File.WriteAllTextAsync(
            path: fileName,
            contents: ValidPackageJson,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<PackageUpdate> result = await this._loader.LoadAsync(
            path: fileName,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "Test.Package", actual: result[0].PackageId);
    }

    [Fact]
    public async Task LoadFromFileWithEmptyArrayThrowsInvalidOperationExceptionAsync()
    {
        string fileName = Path.Combine(path1: this.TempFolder, path2: "packages-empty.json");
        await File.WriteAllTextAsync(
            path: fileName,
            contents: EmptyPackageJson,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken: this.CancellationToken()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: async () =>
            await this._loader.LoadAsync(path: fileName, cancellationToken: this.CancellationToken())
        );
    }

    [Fact]
    public async Task LoadFromFileWithMultiplePackagesReturnsAllAsync()
    {
        const string multiplePackageJson = """
            [
              {"packageId":"Package.A","type":"nuget","exact-match":false,"version-bump-package":false,"prohibit-version-bump-when-referenced":false},
              {"packageId":"Package.B","type":"nuget","exact-match":true,"version-bump-package":false,"prohibit-version-bump-when-referenced":false}
            ]
            """;
        string fileName = Path.Combine(path1: this.TempFolder, path2: "packages-multi.json");
        await File.WriteAllTextAsync(
            path: fileName,
            contents: multiplePackageJson,
            encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken: this.CancellationToken()
        );

        IReadOnlyList<PackageUpdate> result = await this._loader.LoadAsync(
            path: fileName,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 2, actual: result.Count);
    }

    [Fact]
    public async Task LoadFromHttpWithValidContentReturnsPackagesAsync()
    {
        using MockHttpMessageHandler handler = new(ValidPackageJson);
        using HttpClient httpClient = new(handler);
        this._httpClientFactory.CreateClient(name: nameof(BulkPackageConfigLoader)).Returns(httpClient);

        IReadOnlyList<PackageUpdate> result = await this._loader.LoadAsync(
            path: "http://example.com/packages.json",
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task LoadFromHttpWithEmptyArrayThrowsInvalidOperationExceptionAsync()
    {
        using MockHttpMessageHandler handler = new(EmptyPackageJson);
        using HttpClient httpClient = new(handler);
        this._httpClientFactory.CreateClient(name: nameof(BulkPackageConfigLoader)).Returns(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await this._loader.LoadAsync(
                path: "http://example.com/packages.json",
                cancellationToken: this.CancellationToken()
            )
        );
    }
}
