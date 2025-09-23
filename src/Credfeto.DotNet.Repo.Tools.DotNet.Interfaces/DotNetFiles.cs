using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;

[DebuggerDisplay("Folder: {SourceFolder}")]
public readonly record struct DotNetFiles(string SourceFolder, IReadOnlyList<string> Solutions, IReadOnlyList<string> Projects);