using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("Project: {ProjectFileName}, Type: {Type} Name {Name} Version: {Version}")]
internal sealed record ReferenceCheckResult(string ProjectFileName, ReferenceType Type, string Name, string? Version)
{
    public ReferenceCheckResult(string ProjectFileName, ReferenceType Type, string Name)
        : this(ProjectFileName: ProjectFileName, Type: Type, Name: Name, Version: null) { }
};
