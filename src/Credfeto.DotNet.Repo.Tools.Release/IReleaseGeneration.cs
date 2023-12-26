using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build;
using Credfeto.DotNet.Repo.Tools.DotNet;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;

namespace Credfeto.DotNet.Repo.Tools.Release;

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