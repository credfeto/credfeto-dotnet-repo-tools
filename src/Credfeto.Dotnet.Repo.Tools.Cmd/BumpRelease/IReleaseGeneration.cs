using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Tools.Cmd.Build;
using Credfeto.Dotnet.Repo.Tools.Cmd.DotNet;
using Credfeto.Dotnet.Repo.Tools.Cmd.Models;
using Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.BumpRelease;

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