#if NET7_0_OR_GREATER
using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.Cmd.BumpRelease;

internal static partial class ChangeLogParsingRegex
{
    [GeneratedRegex(pattern: REGEX_DEPENDENCIES, options: REGEX_OPTIONS | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS)]
    public static partial Regex Dependencies();

    [GeneratedRegex(pattern: REGEX_GEOIP, options: REGEX_OPTIONS | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS)]
    public static partial Regex GeoIp();

    [GeneratedRegex(pattern: REGEX_DOTNET_SDK, options: REGEX_OPTIONS | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS)]
    public static partial Regex DotNetSdk();
}
#endif