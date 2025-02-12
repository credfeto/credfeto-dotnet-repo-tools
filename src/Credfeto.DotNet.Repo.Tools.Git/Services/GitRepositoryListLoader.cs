using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Git.Services;

public sealed class GitRepositoryListLoader : IGitRepositoryListLoader
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GitRepositoryListLoader(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public ValueTask<IReadOnlyList<string>> LoadAsync(string path, CancellationToken cancellationToken)
    {
        return Uri.TryCreate(uriString: path, uriKind: UriKind.Absolute, out Uri? uri) && IsHttp(uri)
            ? this.LoadFromHttpAsync(uri: uri, cancellationToken: cancellationToken)
            : LoadFromFileAsync(path: path, cancellationToken: cancellationToken);
    }

    private async ValueTask<IReadOnlyList<string>> LoadFromHttpAsync(Uri uri, CancellationToken cancellationToken)
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient(name: nameof(GitRepositoryListLoader));

        httpClient.BaseAddress = uri;

        string result = await httpClient.GetStringAsync(requestUri: uri, cancellationToken: cancellationToken);

        return GetRepos(SplitText(result));
    }

    private static IEnumerable<string> SplitText(string result)
    {
        return result
            .Split(separator: "\r\n", options: StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(r => r.Split(separator: "\n\r", options: StringSplitOptions.RemoveEmptyEntries))
            .SelectMany(r => r.Split(separator: "\n", options: StringSplitOptions.RemoveEmptyEntries))
            .SelectMany(r => r.Split(separator: "\r", options: StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsHttp(Uri uri)
    {
        return StringComparer.Ordinal.Equals(x: uri.Scheme, y: "https") || StringComparer.Ordinal.Equals(x: uri.Scheme, y: "http");
    }

    private static async ValueTask<IReadOnlyList<string>> LoadFromFileAsync(string path, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> lines = await File.ReadAllLinesAsync(path: path, cancellationToken: cancellationToken);

        return GetRepos(lines);
    }

    private static IReadOnlyList<string> GetRepos(IEnumerable<string> lines)
    {
        return [.. lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())];
    }
}
