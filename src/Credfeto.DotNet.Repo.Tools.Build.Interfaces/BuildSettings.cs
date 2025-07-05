using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

[DebuggerDisplay("Publishable: {Publishable}, Packable: {Packable}, Framework: {Framework}")]
public readonly record struct BuildSettings(
    IReadOnlyList<string> PublishableProjects,
    IReadOnlyList<string> PackableProjects,
    string? Framework
)
{
    public bool Publishable => this.PublishableProjects.Count != 0;

    public bool Packable => this.PackableProjects.Count != 0;
}
