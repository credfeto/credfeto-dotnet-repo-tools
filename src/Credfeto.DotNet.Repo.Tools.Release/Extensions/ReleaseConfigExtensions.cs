using System.Collections.Generic;
using System.Linq;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Release.Extensions;

public static class ReleaseConfigExtensions
{
    // ShouldNeverAutoReleaseRepo Looks at ReleaseConfig.NeverRelease
    public static bool ShouldNeverAutoReleaseRepo(
        in this ReleaseConfig releaseConfig,
        string repoClonePath
    )
    {
        return releaseConfig.NeverRelease.MatchesPolicy(
            repoClonePath: repoClonePath,
            policy: false
        );
    }

    // CheckRepoForAllowedAutoUpgrade Looks at (NOT ReleaseConfig.NeverRelease AND ReleaseConfig.AlwaysMatch) OR ReleaseConfig.AllowedAutoUpgrade
    public static bool CheckRepoForAllowedAutoUpgrade(
        in this ReleaseConfig releaseConfig,
        string repoClonePath
    )
    {
        return releaseConfig.ShouldAlwaysCreatePatchRelease(repoClonePath)
            || releaseConfig.AllowedAutoUpgrade.MatchesPolicy(
                repoClonePath: repoClonePath,
                policy: true
            );
    }

    // ShouldAlwaysCreatePatchRelease Looks at (NOT ReleaseConfig.NeverRelease AND ReleaseConfig.AlwaysMatch)
    public static bool ShouldAlwaysCreatePatchRelease(
        in this ReleaseConfig releaseConfig,
        string repoClonePath
    )
    {
        return !releaseConfig.ShouldNeverAutoReleaseRepo(repoClonePath)
            && releaseConfig.AlwaysMatch.MatchesPolicy(repoClonePath: repoClonePath, policy: false);
    }

    private static bool MatchesPolicy(
        this IReadOnlyList<RepoMatch> grouping,
        string repoClonePath,
        bool policy
    )
    {
        return grouping
            .Where(match => match.IsMatch(repoClonePath))
            .Select(match => match.Include)
            .FirstOrDefault(policy);
    }
}
