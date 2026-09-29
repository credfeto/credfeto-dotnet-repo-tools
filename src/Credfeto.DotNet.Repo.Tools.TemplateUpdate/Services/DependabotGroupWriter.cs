using System.Collections.Generic;
using System.Linq;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public static class DependabotGroupWriter
{
    public static void AddGroups(List<string> config, IReadOnlyList<DependabotGroup> groups)
    {
        if (groups is [])
        {
            return;
        }

        config.Add("    groups:");

        foreach (DependabotGroup group in groups)
        {
            config.Add($"      {group.Name}:");
            config.Add("        patterns:");
            config.AddRange(group.Patterns.Select(pattern => $"          - \"{pattern}\""));
        }
    }
}
