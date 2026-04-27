using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

internal sealed class LabelerYamlBuilder
{
    private readonly List<(string Name, string[] Paths)> _entries = [];

    public LabelerYamlBuilder Add(string name, params string[] paths)
    {
        this._entries.Add((name, paths));

        return this;
    }

    public string Build()
    {
        StringBuilder sb = new();

        foreach ((string name, string[] paths) in this._entries)
        {
            string pathList = string.Join(separator: ", ", paths.Select(p => $"'{p}'"));
            sb.AppendLine($"\"{name}\":");
            sb.AppendLine($" - any: [ {pathList} ]");
        }

        return sb.ToString();
    }
}
