using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("Name {PackageId} Version {Version}")]
internal sealed record PackageReference(string PackageId, string Version) : IPackageReference
{
    public FilePackageReference ToFilePackageReference(string baseDir)
    {
        return new(File: baseDir, PackageId: this.PackageId, Version: this.Version);
    }
}