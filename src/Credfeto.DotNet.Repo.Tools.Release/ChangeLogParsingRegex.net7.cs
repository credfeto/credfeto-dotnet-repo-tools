#if NET7_0_OR_GREATER
using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.Release;

internal static partial class ChangeLogParsingRegex
{
    [GeneratedRegex(pattern: REGEX_DEPENDENCIES, REGEX_OPTIONS | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS)]
    public static partial Regex Dependencies();

    [GeneratedRegex(pattern: REGEX_GEOIP, REGEX_OPTIONS | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS)]
    public static partial Regex GeoIp();

    [GeneratedRegex(pattern: REGEX_DOTNET_SDK, REGEX_OPTIONS | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS)]
    public static partial Regex DotNetSdk();
}
#endif
