using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Credfeto.DotNet.Repo.Tools.Git;

public static class RepoUrlParser
{
    public static bool TryParse(
        string path,
        out GitUrlProtocol protocol,
        [NotNullWhen(true)] out string? host
    )
    {
        if (Uri.TryCreate(uriString: path, uriKind: UriKind.Absolute, out Uri? uri))
        {
            if (IsHttp(uri))
            {
                protocol = GitUrlProtocol.HTTP;
                host = uri.Host;

                return true;
            }

            protocol = GitUrlProtocol.UNKNOWN;
            host = null;

            return false;
        }

        Match m = Helpers.GitUrlProtocolRegex.Host().Match(path);

        if (m.Success)
        {
            protocol = GitUrlProtocol.SSH;
            host = m.Groups["Host"].Value;

            return true;
        }

        protocol = GitUrlProtocol.UNKNOWN;
        host = null;

        return false;
    }

    private static bool IsHttp(Uri uri)
    {
        return StringComparer.Ordinal.Equals(x: uri.Scheme, y: "https")
            || StringComparer.Ordinal.Equals(x: uri.Scheme, y: "http");
    }
}
