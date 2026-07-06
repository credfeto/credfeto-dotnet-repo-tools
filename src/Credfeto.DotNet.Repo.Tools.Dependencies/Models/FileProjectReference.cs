using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("File: {File}, Name {RelativeInclude}")]
public sealed record FileProjectReference(string File, string RelativeInclude);
