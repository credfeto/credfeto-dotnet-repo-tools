using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces;

public interface IGitRepositoryFactory
{
    ValueTask<IGitRepository> OpenOrCloneAsync(
        string workDir,
        string repoUrl,
        in CancellationToken cancellationToken
    );
}
