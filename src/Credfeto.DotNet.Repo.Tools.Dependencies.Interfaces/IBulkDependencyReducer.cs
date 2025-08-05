using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;

public interface IBulkDependencyReducer
{
    ValueTask BulkUpdateAsync(
        string templateRepository,
        string trackingFileName,
        string workFolder,
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    );
}
