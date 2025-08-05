using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("File: {File}, Name {PackageId} Version {Version}")]
internal sealed record FilePackageReference(string File, string PackageId, string Version) : IPackageReference;
