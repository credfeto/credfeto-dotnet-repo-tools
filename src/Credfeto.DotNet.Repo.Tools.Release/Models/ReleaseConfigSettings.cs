using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.Release.Models;

[DebuggerDisplay(
    "AutoReleasePendingPackages: {AutoReleasePendingPackages}, MinimumHoursBeforeAutoRelease: {MinimumHoursBeforeAutoRelease}, InactivityHoursBeforeAutoRelease: {InactivityHoursBeforeAutoRelease}"
)]
internal sealed class ReleaseConfigSettings
{
    [JsonConstructor]
    public ReleaseConfigSettings(
        int autoReleasePendingPackages,
        double minimumHoursBeforeAutoRelease,
        double inactivityHoursBeforeAutoRelease
    )
    {
        this.AutoReleasePendingPackages = autoReleasePendingPackages;
        this.MinimumHoursBeforeAutoRelease = minimumHoursBeforeAutoRelease;
        this.InactivityHoursBeforeAutoRelease = inactivityHoursBeforeAutoRelease;
    }

    public int AutoReleasePendingPackages { get; }

    public double MinimumHoursBeforeAutoRelease { get; }

    public double InactivityHoursBeforeAutoRelease { get; }
}
