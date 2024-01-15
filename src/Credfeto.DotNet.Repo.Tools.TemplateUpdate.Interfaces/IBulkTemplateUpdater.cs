using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;

public interface IBulkTemplateUpdater
{
    ValueTask BulkUpdateAsync(string templateRepository,
                              string trackingFileName,
                              string packagesFileName,
                              string workFolder,
                              string releaseConfigFileName,
                              IReadOnlyList<string> repositories,
                              CancellationToken cancellationToken);
}