using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

[DebuggerDisplay("{PackageId}: {ExactMatch}")]
public sealed class PackageExclude
{
    [JsonConstructor]
    public PackageExclude(string packageId, bool exactMatch)
    {
        this.PackageId = packageId;
        this.ExactMatch = exactMatch;
    }

    public string PackageId { get; }

    [JsonPropertyName("exact-match")]
    public bool ExactMatch { get; }
}