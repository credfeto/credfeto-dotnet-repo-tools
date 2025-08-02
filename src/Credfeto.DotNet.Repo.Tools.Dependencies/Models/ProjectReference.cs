using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("Name {RelativeInclude}")]
internal sealed record ProjectReference(string RelativeInclude) : IProjectReference
{
    public FileProjectReference ToFileProjectReference(string baseDir)
    {
        return new(File: baseDir, RelativeInclude: this.RelativeInclude);
    }
}