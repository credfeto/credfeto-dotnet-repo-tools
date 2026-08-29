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
        if (x is null || y is null)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(x, y);
        }

        if (TryGetVersion(tfm: x, out Version? xVersion) && TryGetVersion(tfm: y, out Version? yVersion))
        {
            return xVersion.CompareTo(yVersion);
        }

        bool xIsUnified = TryGetVersion(tfm: x, out _);
        bool yIsUnified = TryGetVersion(tfm: y, out _);

        if (xIsUnified != yIsUnified)
        {
            // A parsed, unified TFM (net5.0+) is always newer than a legacy moniker
            // (e.g. net472, netstandard2.0) that TryGetVersion cannot parse.
            return xIsUnified ? 1 : -1;
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

        return Version.TryParse(input: span, result: out version);
    }
}
