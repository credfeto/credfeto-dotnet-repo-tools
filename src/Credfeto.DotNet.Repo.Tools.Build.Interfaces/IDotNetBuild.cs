using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Build.Interfaces;

public interface IDotNetBuild
{
    ValueTask<BuildSettings> LoadBuildSettingsAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken);

    ValueTask BuildAsync(BuildContext buildContext, CancellationToken cancellationToken);

    ValueTask BuildAsync(string projectFileName, BuildContext buildContext, CancellationToken cancellationToken);
}