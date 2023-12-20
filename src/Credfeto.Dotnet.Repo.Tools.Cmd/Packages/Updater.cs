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

    public static async Task UpdateRepositoriesAsync(string workFolder,
                                                     IReadOnlyList<string> additionalSources,
                                                     string? cacheFile,
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
                await UpdateRepositoryAsync(workFolder: workFolder,
                                            additionalSources: additionalSources,
                                            logging: logging,
                                            packages: packages,
                                            packageUpdater: packageUpdater,
                                            cancellationToken: cancellationToken,
                                            repo: repo);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(cacheFile))
                {
                    await packageCache.SaveAsync(fileName: cacheFile, cancellationToken: CancellationToken.None);
                }
            }
        }
    }

    private static async Task UpdateRepositoryAsync(string workFolder,
                                                    IReadOnlyList<string> additionalSources,
                                                    IDiagnosticLogger logging,
                                                    IReadOnlyList<PackageUpdate> packages,
                                                    IPackageUpdater packageUpdater,
                                                    string repo,
                                                    CancellationToken cancellationToken)
    {
        logging.LogInformation($"Processing {repo}");

        using (Repository repository = await GitUtils.OpenOrCloneAsync(workDir: workFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            bool first = true;

            foreach (PackageUpdate package in packages)
            {
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