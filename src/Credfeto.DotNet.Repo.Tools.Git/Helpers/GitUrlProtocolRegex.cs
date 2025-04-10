using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.Git.Helpers;

internal static partial class GitUrlProtocolRegex
{
    private const RegexOptions REGEX_OPTIONS =
        RegexOptions.Multiline
        | RegexOptions.Compiled
        | RegexOptions.CultureInvariant
        | RegexOptions.ExplicitCapture
        | RegexOptions.NonBacktracking;
    private const int REGEX_TIMEOUT_MILLISECONDS = 1000;

    [GeneratedRegex(
        pattern: "@(?<Host>.*):",
        REGEX_OPTIONS,
        matchTimeoutMilliseconds: REGEX_TIMEOUT_MILLISECONDS
    )]
    public static partial Regex Host();
}
