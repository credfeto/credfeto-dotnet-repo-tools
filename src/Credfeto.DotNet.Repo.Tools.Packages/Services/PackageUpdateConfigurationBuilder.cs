using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;
using Credfeto.Package;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services;

public sealed class PackageUpdateConfigurationBuilder : IPackageUpdateConfigurationBuilder
{
    private readonly ILogger<PackageUpdateConfigurationBuilder> _logger;

    public PackageUpdateConfigurationBuilder(ILogger<PackageUpdateConfigurationBuilder> logger)
    {
        this._logger = logger;
    }

    public PackageUpdateConfiguration Build(PackageUpdate package)
    {
        PackageMatch packageMatch = new(PackageId: package.PackageId, Prefix: !package.ExactMatch);
        this._logger.LogIncludingPackage(packageMatch);

        IReadOnlyList<PackageMatch> excludedPackages = this.GetExcludedPackages(package.Exclude ?? []);

        return new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);
    }

    private IReadOnlyList<PackageMatch> GetExcludedPackages(IReadOnlyList<PackageExclude> excludes)
    {
        if (excludes.Count == 0)
        {
            return [];
        }

        List<PackageMatch> excludedPackages = [];

        foreach (PackageExclude exclude in excludes)
        {
            PackageMatch packageMatch = new(PackageId: exclude.PackageId, Prefix: !exclude.ExactMatch);

            excludedPackages.Add(packageMatch);

            this._logger.LogExcludingPackage(packageMatch);
        }

        return excludedPackages;
    }
}