using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class CleanupTemplateConfig
{
    [JsonConstructor]
    public CleanupTemplateConfig(in Dictionary<string, string> files)
    {
        this.Files = files;
    }

    public Dictionary<string, string> Files { get; }
}
