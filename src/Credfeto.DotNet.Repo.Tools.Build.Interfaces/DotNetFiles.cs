using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

[DebuggerDisplay("Folder: {SourceDirectory} Solutions: {HasSolutions} Projects: {HasProjects}")]
public readonly record struct DotNetFiles(string SourceDirectory, IReadOnlyList<string> Solutions, IReadOnlyList<string> Projects)
{
    public bool HasSolutions => this.Solutions is not [];

    public bool HasProjects => this.Projects is not [];

    public bool HasSolutionsAndProjects => this.HasSolutions && this.HasProjects;
}