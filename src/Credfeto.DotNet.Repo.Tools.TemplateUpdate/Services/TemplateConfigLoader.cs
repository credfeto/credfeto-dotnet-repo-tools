using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Extensions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class TemplateConfigLoader : ITemplateConfigLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TemplateConfigLoader> _logger;

    public TemplateConfigLoader(IHttpClientFactory httpClientFactory, ILogger<TemplateConfigLoader> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    public ValueTask<TemplateConfig> LoadConfigAsync(string path, CancellationToken cancellationToken)
    {
        this._logger.LoadingTemplateConfig(path);

        return Uri.TryCreate(uriString: path, uriKind: UriKind.Absolute, out Uri? uri) && uri.IsHttp()
            ? this.LoadFromHttpAsync(uri: uri, cancellationToken: cancellationToken)
            : LoadFromFileAsync(filename: path, cancellationToken: cancellationToken);
    }

    private async ValueTask<TemplateConfig> LoadFromHttpAsync(Uri uri, CancellationToken cancellationToken)
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient(name: nameof(TemplateConfigLoader));

        httpClient.BaseAddress = uri;

        await using (Stream result = await httpClient.GetStreamAsync(requestUri: uri, cancellationToken: cancellationToken))
        {
            return await JsonSerializer.DeserializeAsync(utf8Json: result, jsonTypeInfo: TemplateConfigSerializationContext.Default.TemplateConfig, cancellationToken: cancellationToken) ??
                   InvalidSettings();
        }
    }

    private static async ValueTask<TemplateConfig> LoadFromFileAsync(string filename, CancellationToken cancellationToken)
    {
        byte[] content = await File.ReadAllBytesAsync(path: filename, cancellationToken: cancellationToken);

        return JsonSerializer.Deserialize(utf8Json: content, jsonTypeInfo: TemplateConfigSerializationContext.Default.TemplateConfig) ?? InvalidSettings();
    }

    private static TemplateConfig InvalidSettings()
    {
        throw new InvalidOperationException("Invalid template settings");
    }
}