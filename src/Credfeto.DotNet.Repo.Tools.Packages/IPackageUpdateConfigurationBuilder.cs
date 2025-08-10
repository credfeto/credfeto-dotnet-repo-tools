using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.Package;

namespace Credfeto.DotNet.Repo.Tools.Packages;

public interface IPackageUpdateConfigurationBuilder
{
    PackageUpdateConfiguration Build(PackageUpdate package);
}