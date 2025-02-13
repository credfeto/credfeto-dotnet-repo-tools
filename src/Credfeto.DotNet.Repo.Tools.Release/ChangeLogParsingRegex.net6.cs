#if NET7_0_OR_GREATER
#else
using System;
using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.Release;

internal static partial class ChangeLogParsingRegex
{
    private static Regex RegexDependencies { get; } =
        new(
            pattern: REGEX_DEPENDENCIES,
            options: REGEX_OPTIONS,
            TimeSpan.FromMilliseconds(value: REGEX_TIMEOUT_MILLISECONDS)
        );

    private static Regex RegexGeoIp { get; } =
        new(
            pattern: REGEX_GEOIP,
            options: REGEX_OPTIONS,
            TimeSpan.FromMilliseconds(value: REGEX_TIMEOUT_MILLISECONDS)
        );

    private static Regex RegexDotNetSdk { get; } =
        new(
            pattern: REGEX_DOTNET_SDK,
            options: REGEX_OPTIONS,
            TimeSpan.FromMilliseconds(value: REGEX_TIMEOUT_MILLISECONDS)
        );

    public static Regex Dependencies()
    {
        return RegexDependencies;
    }

    public static Regex GeoIp()
    {
        return RegexGeoIp;
    }

    public static Regex DotNetSdk()
    {
        return RegexDotNetSdk;
    }
}
#endif
