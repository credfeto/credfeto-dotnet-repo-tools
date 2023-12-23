using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Cmd.Models;
using Credfeto.DotNet.Repo.Tracking;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Packages;

public static class TrackingCacheExtensions
{
    public static async ValueTask UpdateTrackingAsync(this ITrackingCache trackingCache, RepoContext repoContext, UpdateContext updateContext, string? value, CancellationToken cancellationToken)
    {
        trackingCache.Set(repoUrl: repoContext.ClonePath, value: value);

        if (!string.IsNullOrWhiteSpace(updateContext.TrackingFileName))
        {
            await trackingCache.SaveAsync(fileName: updateContext.TrackingFileName, cancellationToken: cancellationToken);
        }
    }
}