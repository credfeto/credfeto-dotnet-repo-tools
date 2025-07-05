using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class DependabotTemplateConfig
{
    [JsonConstructor]
    public DependabotTemplateConfig(bool generate)
    {
        this.Generate = generate;
    }

    public bool Generate { get; }
}
