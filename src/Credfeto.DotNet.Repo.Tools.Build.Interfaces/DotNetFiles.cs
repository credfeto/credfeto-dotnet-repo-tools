using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

[DebuggerDisplay("Folder: {SourceDirectory}")]
public readonly record struct DotNetFiles(string SourceDirectory, IReadOnlyList<string> Solutions, IReadOnlyList<string> Projects);