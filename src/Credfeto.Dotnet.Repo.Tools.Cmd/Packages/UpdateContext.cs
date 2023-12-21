using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dotnet.Repo.Tracking;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

[DebuggerDisplay("Work: {WorkFolder} Additional Sources: {AdditionalSources.Count}")]
public sealed record UpdateContext(string WorkFolder, string? Cache, string Tracking, IReadOnlyList<string> AdditionalSources, ICurrentTimeSource TimeSource, ITrackingCache TrackingCache)
{
    public async ValueTask UpdateTrackingAsync(string repo, string? value, CancellationToken cancellationToken)
    {
        this.TrackingCache.Set(repoUrl: repo, value: value);

        if (!string.IsNullOrWhiteSpace(this.Tracking))
        {
            await this.TrackingCache.SaveAsync(fileName: this.Tracking, cancellationToken: cancellationToken);
        }
    }
}