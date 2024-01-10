using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class BulkTemplateUpdater : IBulkTemplateUpdater
{
    public ValueTask BulkUpdateAsync(string templateRepository,
                                     string trackingFileName,
                                     string packagesFileName,
                                     string workFolder,
                                     string releaseConfigFileName,
                                     IReadOnlyList<string> repositories,
                                     CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Not yet available");
    }
}