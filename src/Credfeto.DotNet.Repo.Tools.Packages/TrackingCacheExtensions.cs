using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tracking.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Packages;

public static class TrackingCacheExtensions
{
    public static async ValueTask UpdateTrackingAsync(
        this ITrackingCache trackingCache,
        RepoContext repoContext,
        PackageUpdateContext updateContext,
        string? value,
        CancellationToken cancellationToken
    )
    {
        trackingCache.Set(repoUrl: repoContext.ClonePath, value: value);

        if (!string.IsNullOrWhiteSpace(updateContext.TrackingFileName))
        {
            await trackingCache.SaveAsync(
                fileName: updateContext.TrackingFileName,
                cancellationToken: cancellationToken
            );
        }
    }
}
