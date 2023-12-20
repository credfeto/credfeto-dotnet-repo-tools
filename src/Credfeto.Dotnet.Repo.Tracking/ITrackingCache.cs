using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dotnet.Repo.Tracking;

public interface ITrackingCache
{
    Task LoadAsync(string fileName, CancellationToken cancellationToken);

    Task SaveAsync(string fileName, CancellationToken cancellationToken);

    string? Get(string repoUrl);

    void Set(string repoUrl, string? value);
}