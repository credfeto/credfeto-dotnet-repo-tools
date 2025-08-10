using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;

namespace Credfeto.DotNet.Repo.Tools.Packages;

public interface ISinglePackageUpdater
{
    ValueTask<bool> UpdateAsync(PackageUpdateContext updateContext,
                                RepoContext repoContext,
                                IReadOnlyList<string> solutions,
                                string sourceDirectory,
                                BuildSettings buildSettings,
                                DotNetVersionSettings dotNetSettings,
                                PackageUpdate package,
                                CancellationToken cancellationToken);
}