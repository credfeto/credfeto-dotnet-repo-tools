using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("General: {General}, GitHub: {GitHub}, DotNet: {DotNet}, Cleanup: {Cleanup}")]
public sealed class TemplateConfig
{
    [JsonConstructor]
    public TemplateConfig(GeneralTemplateConfig general, GitHubTemplateConfig gitHub, DotnetTemplateConfig dotNet, CleanupTemplateConfig cleanup)
    {
        this.General = general;
        this.GitHub = gitHub;
        this.DotNet = dotNet;
        this.Cleanup = cleanup;
    }

    public GeneralTemplateConfig General { get; }

    public GitHubTemplateConfig GitHub { get; }

    public DotnetTemplateConfig DotNet { get; }

    public CleanupTemplateConfig Cleanup { get; }
}