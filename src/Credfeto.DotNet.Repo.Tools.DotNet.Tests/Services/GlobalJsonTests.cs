using System;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Tests.Services;

public sealed class GlobalJsonTests : LoggingTestBase, IDisposable
{
    private readonly string _baseFolder;
    private readonly IGlobalJson _globalJson;

    public GlobalJsonTests(ITestOutputHelper output)
        : base(output)
    {
        this._baseFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(this._baseFolder, "src"));
        this._globalJson = new GlobalJson();
    }

    public void Dispose()
    {
        if (Directory.Exists(this._baseFolder))
        {
            Directory.Delete(path: this._baseFolder, recursive: true);
        }
    }

    [Theory]
    [InlineData("9.0.100", true, "latestMajor")]
    [InlineData("10.0.0", false, "latestFeature")]
    public async Task LoadGlobalJsonWithAllFieldsReturnsCorrectSettingsAsync(
        string version,
        bool allowPrerelease,
        string rollForward
    )
    {
        string json = $$"""
            {
              "sdk": {
                "version": "{{version}}",
                "allowPrerelease": {{allowPrerelease.ToString().ToLowerInvariant()}},
                "rollForward": "{{rollForward}}"
              }
            }
            """;

        await this.WriteGlobalJsonAsync(json);

        DotNetVersionSettings settings = await this._globalJson.LoadGlobalJsonAsync(
            baseFolder: this._baseFolder,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: version, actual: settings.SdkVersion);
        Assert.Equal(expected: allowPrerelease, actual: settings.AllowPreRelease);
        Assert.Equal(expected: rollForward, actual: settings.RollForward);
    }

    [Fact]
    public async Task LoadGlobalJsonWithVersionOnlyUsesDefaultsAsync()
    {
        const string json = """
            {
              "sdk": {
                "version": "9.0.100"
              }
            }
            """;

        await this.WriteGlobalJsonAsync(json);

        DotNetVersionSettings settings = await this._globalJson.LoadGlobalJsonAsync(
            baseFolder: this._baseFolder,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "9.0.100", actual: settings.SdkVersion);
        Assert.False(condition: settings.AllowPreRelease, userMessage: "AllowPreRelease should default to false");
        Assert.Equal(expected: "latestPatch", actual: settings.RollForward);
    }

    [Fact]
    public Task LoadGlobalJsonWhenFileIsMissingThrowsAsync()
    {
        return Assert.ThrowsAsync<FileNotFoundException>(() =>
            this
                ._globalJson.LoadGlobalJsonAsync(
                    baseFolder: this._baseFolder,
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task LoadGlobalJsonWhenJsonIsNullThrowsAsync()
    {
        await this.WriteGlobalJsonAsync("null");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            this
                ._globalJson.LoadGlobalJsonAsync(
                    baseFolder: this._baseFolder,
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    [Fact]
    public async Task LoadGlobalJsonWhenSdkIsMissingThrowsAsync()
    {
        await this.WriteGlobalJsonAsync("{}");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            this
                ._globalJson.LoadGlobalJsonAsync(
                    baseFolder: this._baseFolder,
                    cancellationToken: this.CancellationToken()
                )
                .AsTask()
        );
    }

    private Task WriteGlobalJsonAsync(string json)
    {
        string path = Path.Combine(this._baseFolder, "src", "global.json");
        return File.WriteAllTextAsync(path: path, contents: json, cancellationToken: this.CancellationToken());
    }
}
