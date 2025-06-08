using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class GitHubTemplateConfig
{
    [JsonConstructor]
    public GitHubTemplateConfig(bool issueTemplates,
                                bool pullRequestTemplates,
                                bool actions,
                                bool linters,
                                Dictionary<string, string> files,
                                DependabotTemplateConfig dependabot,
                                LabelsTemplateConfig labels)
    {
        this.Files = files;
        this.Dependabot = dependabot;
        this.Labels = labels;
    }

    public Dictionary<string, string> Files { get; }

    public DependabotTemplateConfig Dependabot { get; }

    public LabelsTemplateConfig Labels { get; }

    [JsonPropertyName("issue-templates")]
    public bool IssueTemplates { get; }

    [JsonPropertyName("pr-template")]
    public bool PullRequestTemplates { get; }

    public bool Actions { get; }

    public bool Linters { get; }
}