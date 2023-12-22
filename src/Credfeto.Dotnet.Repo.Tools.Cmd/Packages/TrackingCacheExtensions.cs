using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Tools.Cmd.Models;
using Credfeto.Dotnet.Repo.Tracking;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

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