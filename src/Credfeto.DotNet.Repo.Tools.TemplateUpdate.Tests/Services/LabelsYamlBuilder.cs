using System.Collections.Generic;
using System.Text;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

internal sealed class LabelsYamlBuilder
{
    private readonly List<(string Name, string Color, string Description)> _labels = [];

    public LabelsYamlBuilder Add(string name, string color, string description)
    {
        this._labels.Add((name, color, description));

        return this;
    }

    public string Build()
    {
        StringBuilder sb = new();

        foreach ((string name, string color, string description) in this._labels)
        {
            sb.AppendLine($" - name: \"{name}\"");
            sb.AppendLine($"   color: \"{color}\"");

            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.AppendLine($"   description: \"{description}\"");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
