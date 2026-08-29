using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Credfeto.DotNet.Repo.Tools.Build.Helpers;

public sealed class TargetFrameworkVersionComparer : IComparer<string>
{
    public static readonly TargetFrameworkVersionComparer Instance = new();

    private TargetFrameworkVersionComparer() { }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        if (TryGetVersion(tfm: x, out Version? xVersion) && TryGetVersion(tfm: y, out Version? yVersion))
        {
            return xVersion.CompareTo(yVersion);
        }

        return StringComparer.OrdinalIgnoreCase.Compare(x, y);
    }

    private static bool TryGetVersion(string tfm, [NotNullWhen(true)] out Version? version)
    {
        ReadOnlySpan<char> span = tfm.AsSpan();

        if (!span.StartsWith(value: "net", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            version = null;

            return false;
        }

        span = span[3..];

        int platformSeparator = span.IndexOf('-');

        if (platformSeparator >= 0)
        {
            span = span[..platformSeparator];
        }

        if (!span.Contains('.'))
        {
            version = null;

            return false;
        }

        return Version.TryParse(input: span, result: out version);
    }
}
