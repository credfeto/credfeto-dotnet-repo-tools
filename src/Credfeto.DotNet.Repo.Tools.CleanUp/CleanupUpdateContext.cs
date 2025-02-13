using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

[DebuggerDisplay("Work: {WorkFolder}")]
public readonly record struct CleanupUpdateContext(
    string WorkFolder,
    string TrackingFileName,
    DotNetVersionSettings DotNetSettings,
    ReleaseConfig ReleaseConfig
);
