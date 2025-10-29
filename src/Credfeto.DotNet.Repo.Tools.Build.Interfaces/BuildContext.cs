using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

[DebuggerDisplay("Source: {SourceDirectory}")]
public readonly record struct BuildContext(string SourceDirectory, BuildSettings BuildSettings, BuildOverride BuildOverride);