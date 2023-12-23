using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.Cmd.DotNet.Models;

internal sealed class GlobalJsonSdk
{
    [JsonConstructor]
    public GlobalJsonSdk(string? version, string? rollForward, bool? allowPrerelease)
    {
        this.Version = version;
        this.RollForward = rollForward;
        this.AllowPrerelease = allowPrerelease;
    }

    public string? Version { get; }

    public string? RollForward { get; }

    public bool? AllowPrerelease { get; }
}