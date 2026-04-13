using System;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class PartialFileConfig
{
    [JsonConstructor]
    public PartialFileConfig(string type, PartialFileMatchConfig? match = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        this.Type = type;
        this.Match = match;
    }

    public string Type { get; }

    public PartialFileMatchConfig? Match { get; }
}
