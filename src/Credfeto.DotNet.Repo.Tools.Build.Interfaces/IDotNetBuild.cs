using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

public interface IDotNetBuild
{
    public ValueTask<BuildSettings> LoadBuildSettingsAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken);

    ValueTask BuildAsync(string basePath, BuildSettings buildSettings, BuildOverride buildOverride, CancellationToken cancellationToken);
}