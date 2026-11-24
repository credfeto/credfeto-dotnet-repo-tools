using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

// One command's surface as Cocona showed it. ConsoleAppFramework reads descriptions and short aliases from XML doc
// comments only, and cscleanup strips those, so the help text and the aliases are declared here instead.
[DebuggerDisplay("{Name}")]
internal sealed record CommandLineCommand(string Name, string Description, IReadOnlyList<CommandLineOption> Options)
{
    // Rewrites each short alias (-r) to its long name (--repositories), which is the only name ConsoleAppFramework knows.
    public string[] ExpandShortNames(IReadOnlyList<string> args)
    {
        Dictionary<string, string> longNames = this.Options.ToDictionary(
            keySelector: option => option.ShortForm,
            elementSelector: option => option.LongForm,
            comparer: StringComparer.Ordinal
        );

        bool endOfOptions = false;

        return
        [
            .. args.Select(arg =>
            {
                endOfOptions |= StringComparer.Ordinal.Equals(x: arg, y: CommandLineCompatibility.END_OF_OPTIONS);

                return !endOfOptions && longNames.TryGetValue(key: arg, out string? longName) ? longName : arg;
            }),
        ];
    }
}
