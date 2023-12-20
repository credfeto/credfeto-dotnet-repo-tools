using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Package;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

internal static class Updater
{
    private const string UPSTREAM = "origin";

    public static async Task UpdateRepositoriesAsync(UpdateContext updateContext,
                                                     IReadOnlyList<string> repos,
                                                     IDiagnosticLogger logging,
                                                     IReadOnlyList<PackageUpdate> packages,
                                                     IPackageUpdater packageUpdater,
                                                     IPackageCache packageCache,
                                                     CancellationToken cancellationToken)
    {
        foreach (string repo in repos)
        {
            try
            {
                await UpdateRepositoryAsync(updateContext: updateContext, logging: logging, packages: packages, packageUpdater: packageUpdater, cancellationToken: cancellationToken, repo: repo);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(updateContext.Cache))
                {
                    await packageCache.SaveAsync(fileName: updateContext.Cache, cancellationToken: cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(updateContext.Tracking))
                {
                    await updateContext.TrackingCache.SaveAsync(fileName: updateContext.Tracking, cancellationToken: cancellationToken);
                }
            }
        }
    }

    private static async Task UpdateRepositoryAsync(UpdateContext updateContext,
                                                    IDiagnosticLogger logging,
                                                    IReadOnlyList<PackageUpdate> packages,
                                                    IPackageUpdater packageUpdater,
                                                    string repo,
                                                    CancellationToken cancellationToken)
    {
        logging.LogInformation($"Processing {repo}");

        using (Repository repository = await GitUtils.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            string? lastKnownGoodBuild = updateContext.TrackingCache.Get(repo);

            bool first = true;

            foreach (PackageUpdate package in packages)
            {
                if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, y: repository.Head.Tip.Sha))
                {
                    // TODO: RepoBuild (No package version check)
                    logging.LogInformation("Need to do a Repo build");
                }

                logging.LogInformation($"* Updating {package.PackageId}...");
                PackageUpdateConfiguration config = BuildConfiguration(package);
                IReadOnlyList<PackageVersion> updatesMade = await packageUpdater.UpdateAsync(basePath: repository.Info.WorkingDirectory,
                                                                                             configuration: config,
                                                                                             packageSources: updateContext.AdditionalSources,
                                                                                             cancellationToken: cancellationToken);

                if (updatesMade.Count != 0)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        await GitUtils.ResetToMasterAsync(repo: repository, upstream: UPSTREAM, cancellationToken: cancellationToken);
                    }
                }
            }
        }
    }

    private static PackageUpdateConfiguration BuildConfiguration(PackageUpdate package)
    {
        PackageMatch packageMatch = new(PackageId: package.PackageId, Prefix: !package.ExactMatch);
        Console.WriteLine($"Including {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");

        IReadOnlyList<PackageMatch> excludedPackages = GetExcludedPackages(package.Exclude);

        return new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);
    }

    private static IReadOnlyList<PackageMatch> GetExcludedPackages(IReadOnlyList<PackageExclude> excludes)
    {
        if (excludes.Count == 0)
        {
            return Array.Empty<PackageMatch>();
        }

        List<PackageMatch> excludedPackages = [];

        foreach (PackageExclude exclude in excludes)
        {
            PackageMatch packageMatch = new(PackageId: exclude.PackageId, Prefix: !exclude.ExactMatch);

            excludedPackages.Add(packageMatch);

            Console.WriteLine($"Excluding {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");
        }

        return excludedPackages;
    }
}