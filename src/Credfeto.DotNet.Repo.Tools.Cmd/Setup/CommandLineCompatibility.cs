using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Setup;

// Keeps the observable behaviour of the Cocona command line that ConsoleAppFramework does differently.
internal static class CommandLineCompatibility
{
    private const string END_OF_OPTIONS = "--";

    private static readonly FrozenSet<string> HelpOptions = FrozenSet.Create(StringComparer.Ordinal, "-h", "--help");

    public static bool IsHelpRequest(IReadOnlyList<string> args)
    {
        return args.TakeWhile(arg => !StringComparer.Ordinal.Equals(x: arg, y: END_OF_OPTIONS))
            .Any(HelpOptions.Contains);
    }

    // Cocona printed "<product> <informational version>" for --version.
    public static string ProductVersion(Assembly assembly)
    {
        string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? assembly.GetName().Name
            ?? string.Empty;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? VersionInformation.Version;

        return string.Join(separator: ' ', product, version);
    }

    public static string[] JoinRepeatedOption(IReadOnlyList<string> args, string longName)
    {
        List<string> result = [];
        List<string> values = [];
        int firstValueIndex = -1;

        int index = 0;

        while (index < args.Count)
        {
            string arg = args[index];

            if (StringComparer.Ordinal.Equals(x: arg, y: END_OF_OPTIONS))
            {
                result.AddRange(args.Skip(index));

                break;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(x: arg, y: longName) && index + 1 < args.Count)
            {
                if (firstValueIndex < 0)
                {
                    result.Add(arg);
                    firstValueIndex = result.Count;
                    result.Add(string.Empty);
                }

                values.Add(args[index + 1]);
                index += 2;

                continue;
            }

            result.Add(arg);
            ++index;
        }

        if (firstValueIndex >= 0)
        {
            result[firstValueIndex] = string.Join(separator: ',', values: values);
        }

        return [.. result];
    }
}
