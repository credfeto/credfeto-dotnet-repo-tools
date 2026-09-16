using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class GeneralTemplateConfig
{
    [JsonConstructor]
    [SuppressMessage(
        category: "Style",
        checkId: "IDE0028: Collection initialization can be simplified",
        Justification = "A collection expression cannot pass an IEqualityComparer to the Dictionary constructor; simplifying would silently drop Ordinal and change lookup semantics"
    )]
    public GeneralTemplateConfig(
        Dictionary<string, string> files,
        Dictionary<string, string>? mirrorFolders = null,
        Dictionary<string, PartialFileConfig>? partialFiles = null
    )
    {
        this.Files = files;
        this.MirrorFolders = mirrorFolders ?? new Dictionary<string, string>(System.StringComparer.Ordinal);
        this.PartialFiles = partialFiles ?? new Dictionary<string, PartialFileConfig>(System.StringComparer.Ordinal);
    }

    public Dictionary<string, string> Files { get; }

    [JsonPropertyName("mirror-folders")]
    public Dictionary<string, string> MirrorFolders { get; }

    [JsonPropertyName("partial-files")]
    public Dictionary<string, PartialFileConfig> PartialFiles { get; }
}
