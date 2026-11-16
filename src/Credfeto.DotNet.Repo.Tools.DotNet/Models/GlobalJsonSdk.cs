using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Models;

internal sealed class GlobalJsonSdk
{
    [JsonConstructor]
    public GlobalJsonSdk(string? version, bool? allowPrerelease)
    {
        this.Version = version;
        this.AllowPrerelease = allowPrerelease;
    }

    public string? Version { get; }

    public bool? AllowPrerelease { get; }
}
