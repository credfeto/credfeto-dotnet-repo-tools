using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services;

public sealed class BulkPackageConfigLoader : IBulkPackageConfigLoader
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BulkPackageConfigLoader(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    public ValueTask<IReadOnlyList<PackageUpdate>> LoadAsync(string path, in CancellationToken cancellationToken)
    {
        return Uri.TryCreate(uriString: path, uriKind: UriKind.Absolute, out Uri? uri) && IsHttp(uri)
            ? this.LoadFromHttpAsyncAsync(uri: uri, cancellationToken: cancellationToken)
            : LoadFromFileAsyncAsync(filename: path, cancellationToken: cancellationToken);
    }

    private async ValueTask<IReadOnlyList<PackageUpdate>> LoadFromHttpAsyncAsync(Uri uri, CancellationToken cancellationToken)
    {
        HttpClient httpClient = this._httpClientFactory.CreateClient(name: nameof(BulkPackageConfigLoader));

        httpClient.BaseAddress = uri;

        await using (Stream result = await httpClient.GetStreamAsync(requestUri: uri, cancellationToken: cancellationToken))
        {
            IReadOnlyList<PackageUpdate> packages =
                await JsonSerializer.DeserializeAsync(utf8Json: result,
                                                      jsonTypeInfo: PackageConfigSerializationContext.Default.IReadOnlyListPackageUpdate,
                                                      cancellationToken: cancellationToken) ?? Array.Empty<PackageUpdate>();

            if (packages.Count == 0)
            {
                return NoPackagesFound();
            }

            return packages;
        }
    }

    private static bool IsHttp(Uri uri)
    {
        return StringComparer.Ordinal.Equals(x: uri.Scheme, y: "https") || StringComparer.Ordinal.Equals(x: uri.Scheme, y: "http");
    }

    private static async ValueTask<IReadOnlyList<PackageUpdate>> LoadFromFileAsyncAsync(string filename, CancellationToken cancellationToken)
    {
        byte[] content = await File.ReadAllBytesAsync(path: filename, cancellationToken: cancellationToken);

        IReadOnlyList<PackageUpdate> packages = JsonSerializer.Deserialize(utf8Json: content, jsonTypeInfo: PackageConfigSerializationContext.Default.IReadOnlyListPackageUpdate) ??
                                                Array.Empty<PackageUpdate>();

        if (packages.Count == 0)
        {
            return NoPackagesFound();
        }

        return packages;
    }

    [DoesNotReturn]
    private static IReadOnlyList<PackageUpdate> NoPackagesFound()
    {
        throw new InvalidOperationException("No packages found");
    }
}