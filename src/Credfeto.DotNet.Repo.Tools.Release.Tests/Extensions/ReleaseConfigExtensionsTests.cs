using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using FunFair.Test.Common;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Extensions;

public sealed partial class ReleaseConfigExtensionsTests : TestBase
{
    private readonly ReleaseConfig _releaseConfig;

    public ReleaseConfigExtensionsTests()
    {
        this._releaseConfig = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5,
            InactivityHoursBeforeAutoRelease: 9,
            [
                new(Repo: "template", MatchType: MatchType.CONTAINS, Include: true),
                new(Repo: "git@github.com:example/never-release.git", MatchType: MatchType.EXACT, Include: true),
            ],
            [
                new(Repo: "git@github.com:example/auto-upgrade-true.git", MatchType: MatchType.EXACT, Include: true),
                new(Repo: "git@github.com:example/auto-upgrade-false.git", MatchType: MatchType.EXACT, Include: false),
            ],
            [
                new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true),
                new(Repo: "code-analysis", MatchType: MatchType.CONTAINS, Include: false),
                new(Repo: "git@github.com:example/never-match.git", MatchType: MatchType.EXACT, Include: false),
                new(Repo: "git@github.com:example/always-match.git", MatchType: MatchType.EXACT, Include: true),
            ]
        );
    }
}
