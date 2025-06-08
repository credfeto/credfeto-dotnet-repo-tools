using System.Text.Json.Serialization;
using LanguageExt;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class GeneralTemplateConfig
{
    [JsonConstructor]
    public GeneralTemplateConfig(in Map<string, string> files)
    {
        this.Files = files;
    }

    public Map<string, string> Files { get; }
}