using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("File: {File}, Name {Name}")]
internal sealed record ProjectReference(string File, string Name);