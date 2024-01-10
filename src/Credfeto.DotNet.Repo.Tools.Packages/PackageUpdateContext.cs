using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Packages;

[DebuggerDisplay("Work: {WorkFolder} Additional Sources: {AdditionalSources.Count}")]
public sealed record PackageUpdateContext(
    string WorkFolder,
    string? CacheFileName,
    string TrackingFileName,
    IReadOnlyList<string> AdditionalSources,
    DotNetVersionSettings DotNetSettings,
    ReleaseConfig ReleaseConfig);