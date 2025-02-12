using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Release.Extensions;
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
        this.MockConfig();

        ReleaseConfig config = await this._releaseConfigLoader.LoadAsync(path: "https://www.example.com/release.config", cancellationToken: CancellationToken.None);

        Assert.Equal(expected: 1, actual: config.AutoReleasePendingPackages);
        Assert.Equal(expected: 4, actual: config.MinimumHoursBeforeAutoRelease);
        Assert.Equal(expected: 8, actual: config.InactivityHoursBeforeAutoRelease);

        Assert.NotEmpty(config.NeverRelease);
        Assert.Contains(collection: config.NeverRelease, filter: match => IsMatch(match: match, repo: "template", matchType: MatchType.CONTAINS, include: true));
        Assert.Contains(collection: config.NeverRelease, filter: match => IsMatch(match: match, repo: "git@github.com:example/never-release.git", matchType: MatchType.EXACT, include: true));

        Assert.NotEmpty(config.AlwaysMatch);

        Assert.Contains(collection: config.AlwaysMatch, filter: match => IsMatch(match: match, repo: "git@github.com:example/always-match.git", matchType: MatchType.EXACT, include: false));
        Assert.Contains(collection: config.AlwaysMatch, filter: match => IsMatch(match: match, repo: "code-analysis", matchType: MatchType.CONTAINS, include: true));

        Assert.NotEmpty(config.AllowedAutoUpgrade);

        Assert.Contains(collection: config.AllowedAutoUpgrade, filter: match => IsMatch(match: match, repo: "template", matchType: MatchType.CONTAINS, include: false));
    }

    private static bool IsMatch(in RepoMatch match, string repo, MatchType matchType, bool include)
    {
        return StringComparer.Ordinal.Equals(x: match.Repo, y: repo) && match.MatchType == matchType && match.Include == include;
    }

    [Theory]
    [InlineData("git@github.com:credfeto/cs-template.git")]
    public async Task ShouldNeverAutoReleaseAsync(string repo)
    {
        this.MockConfig();

        ReleaseConfig config = await this._releaseConfigLoader.LoadAsync(path: "https://www.example.com/release.config", cancellationToken: CancellationToken.None);

        Assert.True(config.ShouldNeverAutoReleaseRepo(repo), userMessage: "Should never auto release");
    }

    [Theory]
    [InlineData("git@github.com:credfeto/markridgwellcouk.git")]
    [InlineData("git@github.com:example/code-analysis.git")]
    public async Task ShouldAlwaysCreatePatchReleaseAsync(string repo)
    {
        this.MockConfig();

        ReleaseConfig config = await this._releaseConfigLoader.LoadAsync(path: "https://www.example.com/release.config", cancellationToken: CancellationToken.None);

        Assert.True(config.ShouldAlwaysCreatePatchRelease(repo), userMessage: "Should never auto release");
    }

    [Theory]
    [InlineData("git@github.com:credfeto/markridgwellcouk.git")]
    [InlineData("git@github.com:credfeto/markridgwellcom.git")]
    [InlineData("git@github.com:credfeto/scripts.git")]
    [InlineData("git@github.com:credfeto/LiveBandPhotosCom.git")]
    [InlineData("git@github.com:credfeto/UpdatePackages.git")]
    [InlineData("git@github.com:credfeto/scratch.git")]
    [InlineData("git@github.com:credfeto/auto-update-config.git")]
    [InlineData("git@github.com:credfeto/action-case-checker.git")]
    [InlineData("git@github.com:credfeto/action-dotnet-version-detect.git")]
    [InlineData("git@github.com:credfeto/action-sql-format.git")]
    [InlineData("git@github.com:credfeto/credfeto.git")]
    [InlineData("git@github.com:credfeto/action-yaml-format.git")]
    [InlineData("git@github.com:credfeto/nuget-multi-push.git")]
    [InlineData("git@github.com:credfeto/changelog-manager.git")]
    [InlineData("git@github.com:credfeto/credfeto-notes.git")]
    [InlineData("git@github.com:credfeto/credfeto-notification-bot.git")]
    [InlineData("git@github.com:credfeto/action-no-ignored-files.git")]
    [InlineData("git@github.com:credfeto/action-repo-visibility.git")]
    [InlineData("git@github.com:credfeto/credfeto-config-backup.git")]
    [InlineData("git@github.com:credfeto/credfeto-extensions-configuration-typed-json.git")]
    [InlineData("git@github.com:credfeto/credfeto-enum-source-generation.git")]
    [InlineData("git@github.com:credfeto/credfeto-date.git")]
    [InlineData("git@github.com:credfeto/credfeto-extensions-linq.git")]
    [InlineData("git@github.com:credfeto/credfeto-random.git")]
    [InlineData("git@github.com:credfeto/credfeto-checker.git")]
    [InlineData("git@github.com:credfeto/credfeto-services-startup.git")]
    [InlineData("git@github.com:credfeto/credfeto-database-source-generator.git")]
    [InlineData("git@github.com:credfeto/credfeto-dotnet-repo-tools.git")]
    [InlineData("git@github.com:credfeto/linux-setup.git")]
    [InlineData("git@github.com:credfeto/credfeto-flatpak-filter.git")]
    [InlineData("git@github.com:credfeto/package-logs.git")]
    [InlineData("git@github.com:credfeto/credfeto-systemd.git")]
    [InlineData("git@github.com:credfeto/credfeto-version-constants-generator.git")]
    [InlineData("git@github.com:example/test-repo.git")]
    public async Task CheckRepoForAllowedAutoUpgradeAsync(string repo)
    {
        this.MockConfig();

        ReleaseConfig config = await this._releaseConfigLoader.LoadAsync(path: "https://www.example.com/release.config", cancellationToken: CancellationToken.None);

        Assert.True(config.CheckRepoForAllowedAutoUpgrade(repo), userMessage: "Should check repo for allowed auto upgrade");
    }

    private void MockConfig()
    {
        const string releaseConfigJson = """
            {
                "settings": {
                    "autoReleasePendingPackages": 1,
                    "minimumHoursBeforeAutoRelease": 4,
                    "inactivityHoursBeforeAutoRelease": 8
                },
                "neverRelease": [
                    {
                        "repo": "template",
                        "match": "contains",
                        "include": true
                    },
                    {
                        "repo": "git@github.com:example/never-release.git",
                        "match": "exact",
                        "include": true
                    }
                ],
                "allowedAutoUpgrade": [
                    {
                        "repo": "template",
                        "match": "contains",
                        "include": false
                    },
                    {
                        "repo": "credfeto",
                        "match": "contains",
                        "include": true
                    }
                ],
                "alwaysMatch": [
                    {
                        "repo": "git@github.com:example/always-match.git",
                        "match": "exact",
                        "include": false
                    },
                    {
                        "repo": "code-analysis",
                        "match": "contains",
                        "include": true
                    },
                    {
                        "repo": "credfeto",
                        "match": "contains",
                        "include": true
                    }
                ]
            }
            """;

        this._httpClientFactory.MockCreateClientWithResponse(nameof(ReleaseConfigLoader), httpStatusCode: HttpStatusCode.OK, responseMessage: releaseConfigJson);
    }
}
