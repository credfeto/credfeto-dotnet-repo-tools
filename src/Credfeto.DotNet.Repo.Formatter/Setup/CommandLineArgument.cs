using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

[DebuggerDisplay("{Name}")]
internal sealed record CommandLineArgument(string Name, string Description);
