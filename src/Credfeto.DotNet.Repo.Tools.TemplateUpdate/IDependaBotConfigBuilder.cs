using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public interface IDependaBotConfigBuilder
{
    ValueTask<string> BuildDependabotConfigAsync(RepoContext repoContext, CancellationToken cancellationToken);
}