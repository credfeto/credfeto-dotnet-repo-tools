using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tracking;
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
                    await packageCache.SaveAsync(fileName: updateContext.Cache, cancellationToken: CancellationToken.None);
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
            string? lastKnownGoodBuild = await Status.GetAsync(fileName: updateContext.Tracking, repo: repo, cancellationToken: cancellationToken);

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
                IReadOnlyList<PackageVersion> updatesMade = await UpdateOnePackageInRepoAsync(additionalSources: additionalSources,
                                                                                              logging: logging,
                                                                                              packageUpdater: packageUpdater,
                                                                                              cancellationToken: cancellationToken,
                                                                                              package: package,
                                                                                              repository: repository);

                if (updatesMade.Count != 0)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        await GitUtils.ResetToMasterAsync(repository, UPSTREAM, cancellationToken);
                    }
                }
            }
        }
    }

    private static async Task<IReadOnlyList<PackageVersion>> UpdateOnePackageInRepoAsync(IReadOnlyList<string> additionalSources,
                                                                                         IDiagnosticLogger logging,
                                                                                         IPackageUpdater packageUpdater,
                                                                                         PackageUpdate package,
                                                                                         Repository repository,
                                                                                         CancellationToken cancellationToken)
    {
        logging.LogInformation($"* Updating {package.PackageId}...");
        PackageUpdateConfiguration config = BuildConfiguration(package);
        IReadOnlyList<PackageVersion> updatesMade = await packageUpdater.UpdateAsync(basePath: repository.Info.WorkingDirectory,
                                                                                     configuration: config,
                                                                                     packageSources: additionalSources,
                                                                                     cancellationToken: cancellationToken);

        Console.WriteLine($"Total updates: {updatesMade.Count}");

                if (updatesMade.Count != 0)
                {
                    // TODO: Somewhere in this method check for existing branch for this package

                    try
                    {
                        // TODO: RepoBuild (No package version check)

                        // TODO:
                        GitUtils.Commit(repo: repository, $"Updated {package.PackageId}", currentTimeSource: updateContext.TimeSource);

                        lastKnownGoodBuild = repository.Head.Tip.Sha;
                        await Status.SetAsync(fileName: updateContext.Tracking, repo: repo, value: lastKnownGoodBuild, cancellationToken: cancellationToken);

                        // TODO: Delete All branches for this package update
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);

                        // TODO: Commit to branch, push, switch back to main branch
                    }
                    finally
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            GitUtils.ResetToMaster(repository);
                        }
                    }
                }
            }

            // TODO: Check whether to create a release
        }
        return updatesMade;
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