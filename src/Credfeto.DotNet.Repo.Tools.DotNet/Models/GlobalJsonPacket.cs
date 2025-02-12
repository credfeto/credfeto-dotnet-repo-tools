using System.Text.Json.Serialization;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Models;

internal sealed class GlobalJsonPacket
{
    [JsonConstructor]
    public GlobalJsonPacket(GlobalJsonSdk? sdk)
    {
        this.Sdk = sdk;
    }

    public GlobalJsonSdk? Sdk { get; set; }
}
