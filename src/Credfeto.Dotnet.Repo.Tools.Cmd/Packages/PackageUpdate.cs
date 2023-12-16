using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

[DebuggerDisplay("{PackageId}: {PackageType} Exact: {ExactMatch}")]
public sealed class PackageUpdate
{
    [JsonConstructor]
    public PackageUpdate(string packageId, string packageType, bool exactMatch, bool versionBumpPackage, bool versionBumpWhenReferenced, IReadOnlyList<string> exclude)
    {
        this.PackageId = packageId;
        this.PackageType = packageType;
        this.ExactMatch = exactMatch;
        this.VersionBumpPackage = versionBumpPackage;
        this.VersionBumpWhenReferenced = versionBumpWhenReferenced;
        this.Exclude = exclude;
    }

    public string PackageId { get; }

    [JsonPropertyName("type")]
    public string PackageType { get; }

    [JsonPropertyName("exact-match")]
    public bool ExactMatch { get; }

    [JsonPropertyName("version-bump-package")]
    public bool VersionBumpPackage { get; }

    [JsonPropertyName("prohibit-version-bump-when-referenced")]
    public bool VersionBumpWhenReferenced { get; }

    public IReadOnlyList<string> Exclude { get; }
}