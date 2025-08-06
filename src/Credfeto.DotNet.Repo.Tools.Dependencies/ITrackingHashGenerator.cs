using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;

namespace Credfeto.DotNet.Repo.Tools.Dependencies;

public interface ITrackingHashGenerator
{
    ValueTask<string> GenerateTrackingHashAsync(RepoContext repoContext, CancellationToken cancellationToken);
}
