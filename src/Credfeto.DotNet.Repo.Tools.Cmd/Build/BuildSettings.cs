using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Build;

[DebuggerDisplay("Publishable: {Publishable}, Packable: {Packable}, Framework: {Framework}")]
public readonly record struct BuildSettings(bool Publishable, bool Packable, string? Framework);