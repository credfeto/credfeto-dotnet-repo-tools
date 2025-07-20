using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("File: {File}, Name {Name} Version {Version}")]
internal sealed record PackageReference(string File, string Name, string Version);