using System.Diagnostics;
using System.IO;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

[DebuggerDisplay("File: {File.FullName}, Type: {Type} Name {Name} Version: {Version}")]
internal sealed record ReferenceCheckResult(FileInfo File, ReferenceType Type, string Name, string? Version);