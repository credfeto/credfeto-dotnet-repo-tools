using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Models;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Services;

public sealed class GlobalJson : IGlobalJson
{
    public async ValueTask<DotNetVersionSettings> LoadGlobalJsonAsync(
        string baseFolder,
        CancellationToken cancellationToken
    )
    {
        string path = Path.Combine(path1: baseFolder, path2: "src", path3: "global.json");
        string content = await File.ReadAllTextAsync(path: path, cancellationToken: cancellationToken);
        GlobalJsonPacket p =
            JsonSerializer.Deserialize(
                json: content,
                jsonTypeInfo: GlobalJsonJsonSerializerContext.Default.GlobalJsonPacket
            ) ?? throw new FileNotFoundException(message: "Missing in template global.json", fileName: path);

        GlobalJsonSdk sdk =
            p.Sdk ?? throw new FileNotFoundException(message: "Missing SDK in template global.json", fileName: path);

        return new(SdkVersion: sdk.Version, sdk.AllowPrerelease ?? false, sdk.RollForward ?? "latestPatch");
    }
}
