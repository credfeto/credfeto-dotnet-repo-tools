using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces;

[DebuggerDisplay(
    "AutoReleasePendingPackages: {AutoReleasePendingPackages}, MinimumHoursBeforeAutoRelease: {MinimumHoursBeforeAutoRelease}, InactivityHoursBeforeAutoRelease: {InactivityHoursBeforeAutoRelease}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct ReleaseConfig(
    int AutoReleasePendingPackages,
    double MinimumHoursBeforeAutoRelease,
    double InactivityHoursBeforeAutoRelease,
    IReadOnlyList<RepoMatch> NeverRelease,
    IReadOnlyList<RepoMatch> AllowedAutoUpgrade,
    IReadOnlyList<RepoMatch> AlwaysMatch);