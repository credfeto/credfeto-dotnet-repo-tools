using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("File: {File}, Name {RelativeInclude}")]
internal sealed record FileProjectReference(string File, string RelativeInclude);
