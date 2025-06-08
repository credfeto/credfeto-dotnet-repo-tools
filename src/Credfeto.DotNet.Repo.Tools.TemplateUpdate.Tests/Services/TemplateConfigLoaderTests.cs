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
        this._templateConfigLoader = new TemplateConfigLoader(httpClientFactory: this._httpClientFactory, this.GetTypedLogger<TemplateConfigLoader>());
    }

    [Fact]
    public async Task LoadConfigAsync()
    {
        this.MockConfig();

        TemplateConfig config = await this._templateConfigLoader.LoadConfigAsync(templatePath: "https://example.com/templates.json", this.CancellationToken());
        Assert.Equal(expected: "Test", actual: config.TemplateName);
    }

    private void MockConfig()
    {
        const string releaseConfigJson = """
                                         {
                                           "general": {
                                             ".editorconfig": "Config",
                                             ".csharpierrc": "Config",
                                             ".csharpierrc.json": "Config",
                                             ".csharpierrc.yaml": "Config",
                                             ".gitleaks.toml": "Config",
                                             ".gitignore": "Config",
                                             ".gitattributes": "Config",
                                             ".tsqllintrc": "Linters",
                                             "CONTRIBUTING.md": "Documentation",
                                             "SECURITY.md": "Documentation",
                                             ".github/pr-lint.yml": "Linters",
                                             ".github/CODEOWNERS": "Config",
                                             ".github/PULL_REQUEST_TEMPLATE.md": "Config",
                                             ".github/FUNDING.yml": "Config"
                                           },
                                           "dependabot": {
                                             "rebuild": true
                                           },
                                           "dotnet": {
                                             "global-json": "update"
                                           },
                                           "labels": {
                                             "rebuild": true
                                           },
                                           "github": {
                                             "linters": true,
                                             "actions": true,
                                             "issue-templates": true,
                                             "pr-template": true
                                           },
                                           "remove": {
                                             ".github/actions/nuget-push-integrated-symbol-feed/actions.yml": "Obsolete action",
                                             ".github/actions/nuget-push-separate-symbol-feed/actions.yml": "Obsolete action"
                                           }
                                         }
                                         """;

        this._httpClientFactory.MockCreateClientWithResponse(nameof(TemplateConfigLoader), httpStatusCode: HttpStatusCode.OK, responseMessage: releaseConfigJson);
    }
}