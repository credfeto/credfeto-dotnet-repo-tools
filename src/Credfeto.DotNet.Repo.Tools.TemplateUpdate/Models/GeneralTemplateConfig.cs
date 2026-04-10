using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class GeneralTemplateConfig
{
    [JsonConstructor]
    public GeneralTemplateConfig(
        Dictionary<string, string> files,
        Dictionary<string, string>? mirrorFolders = null,
        Dictionary<string, string>? partialFiles = null
    )
    {
        this.Files = files;
        this.MirrorFolders = mirrorFolders ?? new Dictionary<string, string>(System.StringComparer.Ordinal);
        this.PartialFiles = partialFiles ?? new Dictionary<string, string>(System.StringComparer.Ordinal);
    }

    public Dictionary<string, string> Files { get; }

    [JsonPropertyName("mirror-folders")]
    public Dictionary<string, string> MirrorFolders { get; }

    [JsonPropertyName("partial-files")]
    public Dictionary<string, string> PartialFiles { get; }
}
