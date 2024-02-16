using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public interface IDependaBotConfigBuilder
{
    ValueTask<string> BuildDependabotConfigAsync(RepoContext repoContext,
                                                 string templateFolder,
                                                 IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken);
}