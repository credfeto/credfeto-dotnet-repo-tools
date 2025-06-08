using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("ReGenerate: {Generate}")]
public sealed class LabelsTemplateConfig
{
    [JsonConstructor]
    public LabelsTemplateConfig(bool generate)
    {
        this.Generate = generate;
    }

    public bool Generate { get; }
}