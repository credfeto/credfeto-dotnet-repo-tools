using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.Release;

internal static partial class ChangeLogParsingRegex
{
    private const RegexOptions REGEX_OPTIONS = RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;
    private const int REGEX_TIMEOUT_MILLISECONDS = 1000;

    // "- Dependencies - Updated Microsoft.Extensions.Configuration to 5.0.0"
    private const string REGEX_DEPENDENCIES = @"^\s*\-\s*Dependencies\s*\-\s*Updated\s+(?<PackageId>.+(\.+)*?)\sto\s+(\d+\..*)$";

    // "- GEOIP - "
    private const string REGEX_GEOIP = @"^\-\sGEOIP\s\-\s";

    // "- SDK - Updated DotNet SDK to "
    private const string REGEX_DOTNET_SDK = @"^\s*\-\s*SDK\s*-\s*Updated\s*DotNet\s*SDK\s*to\s*";
}