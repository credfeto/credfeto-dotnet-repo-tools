using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Release.Services;

public sealed class ReleaseConfigLoader : IReleaseConfigLoader
{
    private static readonly IReadOnlyList<RepoMatch> AllowedAutoUpgrade =
    [
        new(Repo: "git@github.com:funfair-tech/funfair-server-content-package.git", MatchType: MatchType.EXACT, Include: false),
        new(Repo: "code-analysis", MatchType: MatchType.CONTAINS, Include: false)
    ];

    private static readonly IReadOnlyList<RepoMatch> AlwaysMatch =
    [
        new(Repo: "template", MatchType: MatchType.CONTAINS, Include: false),
        new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "BuildBot", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "CoinBot", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-server-balance-bot", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-build-check", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-build-version", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "funfair-content-package-builder", MatchType: MatchType.CONTAINS, Include: true)
    ];

    // TODO move to config

    public static readonly IReadOnlyList<RepoMatch> NeverRelease =
    [
        new(Repo: "template", MatchType: MatchType.CONTAINS, Include: true),
        new(Repo: "git@github.com:funfair-tech/funfair-server-content-package.git", MatchType: MatchType.EXACT, Include: true)
    ];

    public ValueTask<ReleaseConfig> LoadAsync(string path, CancellationToken cancellationToken)
    {
        ReleaseConfig releaseConfig = new(AutoReleasePendingPackages: 2,
                                          MinimumHoursBeforeAutoRelease: 4,
                                          InactivityHoursBeforeAutoRelease: 8,
                                          NeverRelease: NeverRelease,
                                          AllowedAutoUpgrade: AllowedAutoUpgrade,
                                          AlwaysMatch: AlwaysMatch);

        return ValueTask.FromResult(releaseConfig);
    }
}