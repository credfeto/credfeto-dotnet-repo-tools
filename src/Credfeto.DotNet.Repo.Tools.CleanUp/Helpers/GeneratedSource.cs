using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Helpers;

internal static class GeneratedSource
{
    private static readonly IReadOnlyList<string> Markers =
    [
        Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
        Path.DirectorySeparatorChar + "generated" + Path.DirectorySeparatorChar,
        ".generated.",
    ];

    public static bool IsNonGenerated(string filename)
    {
        return !IsGenerated(filename);
    }

    public static bool IsGenerated(string filename)
    {
        return Markers.Any(x =>
            filename.Contains(value: x, comparisonType: StringComparison.Ordinal)
        );
    }
}
