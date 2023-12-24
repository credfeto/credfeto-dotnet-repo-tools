using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Build;

public interface IDotNetBuild
{
    public ValueTask<BuildSettings> LoadBuildSettingsAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken);

    ValueTask BuildAsync(string basePath, BuildSettings buildSettings, CancellationToken cancellationToken);
}