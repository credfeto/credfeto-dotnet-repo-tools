using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models.Packages;

namespace Credfeto.DotNet.Repo.Tools.Packages.Interfaces;

public interface IBulkPackageConfigLoader
{
    ValueTask<IReadOnlyList<PackageUpdate>> LoadAsync(string path, in CancellationToken cancellationToken);
}