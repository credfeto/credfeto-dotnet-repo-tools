using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.Release.Models;

[DebuggerDisplay("{Settings.AutoReleasePendingPackages}, {Settings.MinimumHoursBeforeAutoRelease}, {Settings.InactivityHoursBeforeAutoRelease}")]
internal sealed class ReleaseConfiguration
{
    [JsonConstructor]
    public ReleaseConfiguration(
        ReleaseConfigSettings settings,
        IReadOnlyList<RepoConfigMatch> neverRelease,
        IReadOnlyList<RepoConfigMatch> allowedAutoUpgrade,
        IReadOnlyList<RepoConfigMatch> alwaysMatch
    )
    {
        this.Settings = settings;
        this.NeverRelease = neverRelease;
        this.AllowedAutoUpgrade = allowedAutoUpgrade;
        this.AlwaysMatch = alwaysMatch;
    }

    public ReleaseConfigSettings Settings { get; }

    public IReadOnlyList<RepoConfigMatch> NeverRelease { get; }

    public IReadOnlyList<RepoConfigMatch> AllowedAutoUpgrade { get; }

    public IReadOnlyList<RepoConfigMatch> AlwaysMatch { get; }
}
