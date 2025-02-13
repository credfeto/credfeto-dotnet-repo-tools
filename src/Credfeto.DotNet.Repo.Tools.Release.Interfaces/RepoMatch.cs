using System;
using System.Diagnostics;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces;

[DebuggerDisplay("{Repo}: {MatchType} Include : {Include}")]
public readonly record struct RepoMatch(string Repo, MatchType MatchType, bool Include)
{
    public bool IsMatch(string repo)
    {
        return this.MatchType == MatchType.EXACT
            ? StringComparer.OrdinalIgnoreCase.Equals(x: repo, y: this.Repo)
            : repo.Contains(value: this.Repo, comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
