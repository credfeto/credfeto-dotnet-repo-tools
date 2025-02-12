using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;

public interface IBulkCodeCleanUp
{
    ValueTask BulkUpdateAsync(
        string templateRepository,
        string trackingFileName,
        string packagesFileName,
        string workFolder,
        string releaseConfigFileName,
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    );
}
