using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class TemplateConfigLoaderFileTests : LoggingTestBase, IDisposable
{
    private readonly string _tempFolder;
    private readonly ITemplateConfigLoader _templateConfigLoader;

    public TemplateConfigLoaderFileTests(ITestOutputHelper output)
        : base(output)
    {
        this._tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._tempFolder);

        this._templateConfigLoader = new TemplateConfigLoader(
            httpClientFactory: GetSubstitute<IHttpClientFactory>(),
            logger: this.GetTypedLogger<TemplateConfigLoader>()
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempFolder))
        {
            Directory.Delete(path: this._tempFolder, recursive: true);
        }
    }

    [Fact]
    public async Task LoadConfigFromLocalFileAsync()
    {
        const string json = """
            {
              "general": {
                "files": {
                  ".editorconfig": "Config",
                  ".gitignore": "Config"
                }
              },
              "dotNet": {
                "global-json": true,
                "resharper-dotsettings": true,
                "files": {}
              },
              "gitHub": {
                "dependabot": {
                  "generate": true
                },
                "labels": {
                  "generate": true
                },
                "issue-templates": true,
                "pr-template": true,
                "actions": true,
                "linters": true,
                "files": {}
              },
              "cleanup": {
                "files": {}
              }
            }
            """;

        string configFile = Path.Combine(this._tempFolder, "templates.json");
        await File.WriteAllTextAsync(path: configFile, contents: json, cancellationToken: this.CancellationToken());

        TemplateConfig config = await this._templateConfigLoader.LoadConfigAsync(
            path: configFile,
            cancellationToken: this.CancellationToken()
        );

        Assert.NotEmpty(config.General.Files);
        Assert.True(config.DotNet.GlobalJson, userMessage: "GlobalJson should be true");
    }
}
