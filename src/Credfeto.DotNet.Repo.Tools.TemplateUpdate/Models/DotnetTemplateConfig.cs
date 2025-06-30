using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class DotnetTemplateConfig
{
    [JsonConstructor]
    public DotnetTemplateConfig(bool globalJson, bool jetBrainsDotSettings, Dictionary<string, string> files)
    {
        this.GlobalJson = globalJson;
        this.JetBrainsDotSettings = jetBrainsDotSettings;
        this.Files = files;
    }

    public Dictionary<string, string> Files { get; }

    [JsonPropertyName("global-json")]
    public bool GlobalJson { get; }

    [JsonPropertyName("resharper-dotsettings")]
    public bool JetBrainsDotSettings { get; }
}