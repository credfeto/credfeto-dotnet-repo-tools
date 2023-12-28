using System.Linq;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Release.Extensions;

public static class ReleaseConfigExtensions
{
    public static bool ShouldNeverAutoReleaseRepo(in this ReleaseConfig releaseConfig, RepoContext repoContext)
    {
        return releaseConfig.NeverRelease.Where(match => match.IsMatch(repoContext.ClonePath))
                            .Select(match => match.Include)
                            .FirstOrDefault();
    }

    public static bool CheckRepoForAllowedAutoUpgrade(in this ReleaseConfig releaseConfig, RepoContext repoContext)
    {
        return releaseConfig.AllowedAutoUpgrade.Where(match => match.IsMatch(repoContext.ClonePath))
                            .Select(match => match.Include)
                            .FirstOrDefault(true);
    }

    public static bool ShouldAlwaysCreatePatchRelease(in this ReleaseConfig releaseConfig, RepoContext repoContext)
    {
        return releaseConfig.AlwaysMatch.Where(match => match.IsMatch(repoContext.ClonePath))
                            .Select(match => match.Include)
                            .FirstOrDefault();
    }
}