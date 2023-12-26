using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tracking;

public interface ITrackingCache
{
    ValueTask LoadAsync(string fileName, CancellationToken cancellationToken);

    ValueTask SaveAsync(string fileName, CancellationToken cancellationToken);

    string? Get(string repoUrl);

    void Set(string repoUrl, string? value);
}