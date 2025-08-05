namespace Credfeto.DotNet.Repo.Tools.Dependencies.Models;

internal interface IPackageReference
{
    string PackageId { get; }

    string Version { get; }
}
