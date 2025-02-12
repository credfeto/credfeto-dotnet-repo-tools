using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

[DebuggerDisplay("Pre-Release: {PreRelease}")]
public readonly record struct BuildOverride(bool PreRelease);
