using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.Cmd.Models;
using Credfeto.DotNet.Repo.Tools.Cmd.Packages;
using Credfeto.DotNet.Repo.Tools.DotNet;

namespace Credfeto.DotNet.Repo.Tools.Cmd.BumpRelease;

public interface IReleaseGeneration
{
    ValueTask TryCreateNextPatchAsync(RepoContext repoContext,
                                      string basePath,
                                      BuildSettings buildSettings,
                                      DotNetVersionSettings dotNetSettings,
                                      IReadOnlyList<string> solutions,
                                      IReadOnlyList<PackageUpdate> packages,
                                      CancellationToken cancellationToken);

    ValueTask CreateAsync(RepoContext repoContext, CancellationToken cancellationToken);
}