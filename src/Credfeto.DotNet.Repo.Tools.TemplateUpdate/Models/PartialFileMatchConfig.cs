using System;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

[DebuggerDisplay("Begin: {Begin}, End: {End}")]
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
