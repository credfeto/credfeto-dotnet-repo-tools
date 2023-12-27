using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.Models.Packages;

[DebuggerDisplay("{PackageId}: {PackageType} Exact: {ExactMatch}")]
public sealed class PackageUpdate
{
    [JsonConstructor]
    public PackageUpdate(string packageId,
                         string packageType,
                         bool exactMatch,
                         bool versionBumpPackage,
                         bool prohibitVersionBumpWhenReferenced,
                         IReadOnlyList<PackageExclude>? exclude)
    {
        this.PackageId = packageId;
        this.PackageType = packageType;
        this.ExactMatch = exactMatch;
        this.VersionBumpPackage = versionBumpPackage;
        this.ProhibitVersionBumpWhenReferenced = prohibitVersionBumpWhenReferenced;
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
    public bool ProhibitVersionBumpWhenReferenced { get; }

    public IReadOnlyList<PackageExclude>? Exclude { get; }
}