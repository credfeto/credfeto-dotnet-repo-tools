using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class GitHubTemplateConfig
{
    [JsonConstructor]
    public GitHubTemplateConfig(Dictionary<string, string> files, DependabotTemplateConfig dependabot, LabelsTemplateConfig labels)
    {
        this.Files = files;
        this.Dependabot = dependabot;
        this.Labels = labels;
    }

    public Dictionary<string, string> Files { get; }

    public DependabotTemplateConfig Dependabot { get; }

    public LabelsTemplateConfig Labels { get; }
}