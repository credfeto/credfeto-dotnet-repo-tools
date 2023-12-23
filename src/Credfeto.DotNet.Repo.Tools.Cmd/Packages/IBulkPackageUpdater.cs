using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Packages;

public interface IBulkPackageUpdater

{
    ValueTask BulkUpdateAsync(Options options,
        string templateRepository,
        string? cacheFileName,
        string trackingFileName,
        string packagesFileName,
        string workFolder,
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken);

    ValueTask UpdateRepositoriesAsync(UpdateContext updateContext, IReadOnlyList<string> repositories, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken);
}