using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Dependencies.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services;

public sealed class BulkDependencyReducer : IBulkDependencyReducer
{
    public ValueTask BulkUpdateAsync(string templateRepository,
                                     string trackingFileName,
                                     string packagesFileName,
                                     string workFolder,
                                     string releaseConfigFileName,
                                     IReadOnlyList<string> repositories,
                                     CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}