using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class GeneralTemplateConfig
{
    [JsonConstructor]
    public GeneralTemplateConfig(Dictionary<string, string> files)
    {
        this.Files = files;
    }

    public Dictionary<string, string> Files { get; }
}
