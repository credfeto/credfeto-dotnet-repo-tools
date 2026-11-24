using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Credfeto.DotNet.Repo.Formatter.Setup;

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
}
