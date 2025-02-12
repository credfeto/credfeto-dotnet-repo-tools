using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Git.Interfaces;

public interface IGitRepositoryListLoader
{
    ValueTask<IReadOnlyList<string>> LoadAsync(string path, CancellationToken cancellationToken);
}
