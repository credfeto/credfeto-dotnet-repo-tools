using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.Release.Models;

[DebuggerDisplay("{Repository}: {Match} Include: {Include}")]
internal sealed class RepoConfigMatch
{
    [JsonConstructor]
    public RepoConfigMatch(string repository, string match, bool include)
    {
        this.Repository = repository;
        this.Match = match;
        this.Include = include;
    }

    [JsonPropertyName("repo")]
    public string Repository { get; }

    [JsonPropertyName("match")]
    public string Match { get; }

    [JsonPropertyName("include")]
    public bool Include { get; }
}