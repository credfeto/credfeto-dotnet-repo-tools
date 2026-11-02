using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

[DebuggerDisplay("Work: {WorkFolder}")]
public readonly record struct CleanupUpdateContext(string WorkFolder, string TrackingFileName);
