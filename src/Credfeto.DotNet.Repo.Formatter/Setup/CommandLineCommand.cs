using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

// One command's surface as Cocona showed it. ConsoleAppFramework reads descriptions and short aliases from XML doc
// comments only, and cscleanup strips those, so the help text is declared here instead.
[DebuggerDisplay("{Name}")]
internal sealed record CommandLineCommand(
    string Name,
    string Description,
    IReadOnlyList<CommandLineOption> Options,
    CommandLineArgument? Argument
);
