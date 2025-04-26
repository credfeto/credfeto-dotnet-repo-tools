using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Models;
using Credfeto.DotNet.Repo.Tools.Release.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using MatchType = Credfeto.DotNet.Repo.Tools.Release.Interfaces.MatchType;

namespace Credfeto.DotNet.Repo.Tools.Release.Services;

public sealed class ReleaseConfigLoader : IReleaseConfigLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReleaseConfigLoader> _logger;

    public ReleaseConfigLoader(IHttpClientFactory httpClientFactory, ILogger<ReleaseConfigLoader> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    public ValueTask<ReleaseConfig> LoadAsync(string path, CancellationToken cancellationToken)
    {
        this._logger.LoadingReleaseConfig(path);

        return Uri.TryCreate(uriString: path, uriKind: UriKind.Absolute, out Uri? uri) && IsHttp(uri)
            ? this.LoadFromHttpAsync(uri: uri, cancellationToken: cancellationToken)
            : LoadFromFileAsync(filename: path, cancellationToken: cancellationToken);
    }

    private async ValueTask<ReleaseConfig> LoadFromHttpAsync(Uri uri, CancellationToken cancellationToken)
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient(name: nameof(ReleaseConfigLoader));

        httpClient.BaseAddress = uri;

        await using (
            Stream result = await httpClient.GetStreamAsync(requestUri: uri, cancellationToken: cancellationToken)
        )
        {
            ReleaseConfiguration releaseConfiguration =
                await JsonSerializer.DeserializeAsync(
                    utf8Json: result,
                    jsonTypeInfo: ReleaseConfigSerializationContext.Default.ReleaseConfiguration,
                    cancellationToken: cancellationToken
                ) ?? InvalidSettings();

            return ToConfig(releaseConfiguration);
        }
    }

    private static bool IsHttp(Uri uri)
    {
        return StringComparer.Ordinal.Equals(x: uri.Scheme, y: "https")
            || StringComparer.Ordinal.Equals(x: uri.Scheme, y: "http");
    }

    private static async ValueTask<ReleaseConfig> LoadFromFileAsync(
        string filename,
        CancellationToken cancellationToken
    )
    {
        byte[] content = await File.ReadAllBytesAsync(path: filename, cancellationToken: cancellationToken);

        ReleaseConfiguration releaseConfiguration =
            JsonSerializer.Deserialize(
                utf8Json: content,
                jsonTypeInfo: ReleaseConfigSerializationContext.Default.ReleaseConfiguration
            ) ?? InvalidSettings();

        return ToConfig(releaseConfiguration);
    }

    [DoesNotReturn]
    private static ReleaseConfiguration InvalidSettings()
    {
        throw new InvalidOperationException("Invalid release settings");
    }

    private static ReleaseConfig ToConfig(ReleaseConfiguration configuration)
    {
        return new(
            AutoReleasePendingPackages: configuration.Settings.AutoReleasePendingPackages,
            MinimumHoursBeforeAutoRelease: configuration.Settings.MinimumHoursBeforeAutoRelease,
            InactivityHoursBeforeAutoRelease: configuration.Settings.InactivityHoursBeforeAutoRelease,
            ToConfig(configuration.NeverRelease),
            ToConfig(configuration.AllowedAutoUpgrade),
            ToConfig(configuration.AlwaysMatch)
        );
    }

    private static IReadOnlyList<RepoMatch> ToConfig(IReadOnlyList<RepoConfigMatch> source)
    {
        return [.. source.Select(ToConfig)];
    }

    private static RepoMatch ToConfig(RepoConfigMatch source)
    {
        return new(Repo: source.Repository, ExtractMatchType(source.Match), Include: source.Include);
    }

    private static MatchType ExtractMatchType(string sourceMatch)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(x: sourceMatch, y: "exact"))
        {
            return MatchType.EXACT;
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(x: sourceMatch, y: "contains"))
        {
            return MatchType.CONTAINS;
        }

        throw new ArgumentOutOfRangeException(
            nameof(sourceMatch),
            actualValue: sourceMatch,
            message: "Invalid match type"
        );
    }
}
