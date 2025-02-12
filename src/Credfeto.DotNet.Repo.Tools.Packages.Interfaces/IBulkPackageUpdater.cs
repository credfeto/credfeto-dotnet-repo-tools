using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Packages.Interfaces;

public interface IBulkPackageUpdater
{
    ValueTask BulkUpdateAsync(
        string templateRepository,
        string? cacheFileName,
        string trackingFileName,
        string packagesFileName,
        string workFolder,
        string releaseConfigFileName,
        IReadOnlyList<string> additionalNugetSources,
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    );
}
