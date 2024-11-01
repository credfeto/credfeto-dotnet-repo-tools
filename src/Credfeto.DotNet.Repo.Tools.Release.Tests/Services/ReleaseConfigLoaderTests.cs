using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Services;
using FunFair.Test.Common;
using FunFair.Test.Common.Extensions;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Services;

public sealed class ReleaseConfigLoaderTests : TestBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReleaseConfigLoader _releaseConfigLoader;

    public ReleaseConfigLoaderTests()
    {
        this._httpClientFactory = GetSubstitute<IHttpClientFactory>();
        this._releaseConfigLoader = new ReleaseConfigLoader(this._httpClientFactory);
    }

    [Fact]
    public async Task LoadFromUrlAsync()
    {
        const string releaseConfigJson =
            "{\n\"settings\": {\n\"autoReleasePendingPackages\": 1,\n\"minimumHoursBeforeAutoRelease\": 4,\n\"inactivityHoursBeforeAutoRelease\": 8\n},\n\"neverRelease\": [\n{\n\"repo\": \"template\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"git@github.com:example/never-release.git\",\n\"match\": \"exact\",\n\"include\": true\n}\n],\n\"allowedAutoUpgrade\": [\n{\n\"repo\": \"template\",\n\"match\": \"contains\",\n\"include\": false\n},\n{\n\"repo\": \"credfeto\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"BuildBot\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"CoinBot\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"funfair-server-balance-bot\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"funfair-server-build-check\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"funfair-server-build-version\",\n\"match\": \"contains\",\n\"include\": true\n},\n{\n\"repo\": \"funfair-content-package-builder\",\n\"match\": \"contains\",\n\"include\": true\n}\n],\n\"alwaysMatch\": [\n{\n\"repo\": \"git@github.com:funfair-tech/funfair-server-content-package.git\",\n\"match\": \"exact\",\n\"include\": false\n},\n{\n\"repo\": \"code-analysis\",\n\"match\": \"contains\",\n\"include\": false\n}\n]\n}";

        this._httpClientFactory.MockCreateClientWithResponse(nameof(ReleaseConfigLoader), httpStatusCode: HttpStatusCode.OK, responseMessage: releaseConfigJson);

        ReleaseConfig config = await this._releaseConfigLoader.LoadAsync(path: "https://www.example.com/release.config", cancellationToken: CancellationToken.None);

        Assert.Equal(expected: 1, actual: config.AutoReleasePendingPackages);
        Assert.Equal(expected: 4, actual: config.MinimumHoursBeforeAutoRelease);
        Assert.Equal(expected: 8, actual: config.InactivityHoursBeforeAutoRelease);

        Assert.NotEmpty(config.NeverRelease);
        Assert.Contains(collection: config.NeverRelease, filter: x => StringComparer.Ordinal.Equals(x: x.Repo, y: "template") && x is { MatchType: MatchType.CONTAINS, Include: true });
        Assert.Contains(collection: config.NeverRelease,
                        filter: x => StringComparer.Ordinal.Equals(x: x.Repo, y: "git@github.com:example/never-release.git") && x is { MatchType: MatchType.EXACT, Include: true });

        Assert.NotEmpty(config.AlwaysMatch);

        Assert.Contains(collection: config.AlwaysMatch, filter: x => StringComparer.Ordinal.Equals(x: x.Repo, y: "code-analysis") && x is { MatchType: MatchType.CONTAINS, Include: false });

        Assert.NotEmpty(config.AllowedAutoUpgrade);

        Assert.Contains(collection: config.AllowedAutoUpgrade, filter: x => StringComparer.Ordinal.Equals(x: x.Repo, y: "template") && x is { MatchType: MatchType.CONTAINS, Include: false });
    }
}