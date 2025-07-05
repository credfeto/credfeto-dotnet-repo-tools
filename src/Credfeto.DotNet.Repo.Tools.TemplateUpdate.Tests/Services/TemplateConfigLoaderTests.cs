using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using FunFair.Test.Common.Extensions;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class TemplateConfigLoaderTests : TestBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITemplateConfigLoader _templateConfigLoader;

    public TemplateConfigLoaderTests()
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();
        this._templateConfigLoader = new TemplateConfigLoader(
            httpClientFactory: this._httpClientFactory,
            this.GetTypedLogger<TemplateConfigLoader>()
        );
    }

    [Fact]
    public async Task LoadConfigAsync()
    {
        this.MockConfig();

        TemplateConfig config = await this._templateConfigLoader.LoadConfigAsync(
            path: "https://example.com/templates.json",
            this.CancellationToken()
        );
        Assert.NotEmpty(config.General.Files);
    }

    private void MockConfig()
    {
        const string releaseConfigJson = """
            {
              "general": {
                "files": {
                  ".editorconfig": "Config",
                  ".gitignore": "Config",
                  ".gitattributes": "Config"
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
                "files": {
                  ".github/actions/nuget-push-integrated-symbol-feed/actions.yml": "Obsolete action",
                  ".github/actions/nuget-push-separate-symbol-feed/actions.yml": "Obsolete action"
                }
              }
            }
            """;

        this._httpClientFactory.MockCreateClientWithResponse(
            nameof(TemplateConfigLoader),
            httpStatusCode: HttpStatusCode.OK,
            responseMessage: releaseConfigJson
        );
    }
}
