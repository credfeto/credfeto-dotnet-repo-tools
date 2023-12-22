using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

public interface IUpdater
{
    ValueTask UpdateRepositoriesAsync(UpdateContext updateContext, IReadOnlyList<string> repositories, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken);
}