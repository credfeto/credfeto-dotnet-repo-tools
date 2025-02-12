using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces;

public interface IReleaseConfigLoader
{
    ValueTask<ReleaseConfig> LoadAsync(string path, CancellationToken cancellationToken);
}
