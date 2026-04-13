using System;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

public sealed class PartialFileMatchConfig
{
    [JsonConstructor]
    public PartialFileMatchConfig(string begin, string end)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(begin);
        ArgumentException.ThrowIfNullOrWhiteSpace(end);

        this.Begin = begin;
        this.End = end;
    }

    public string Begin { get; }

    public string End { get; }
}
