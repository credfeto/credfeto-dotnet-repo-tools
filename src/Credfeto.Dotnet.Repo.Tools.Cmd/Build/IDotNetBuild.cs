using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Build;

public interface IDotNetBuild
{
    public BuildSettings LoadBuildSettings(IReadOnlyList<string> projects);

    ValueTask BuildAsync(string basePath, BuildSettings buildSettings, CancellationToken cancellationToken);
}