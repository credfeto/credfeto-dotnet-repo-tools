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

        using (Repository repository = GitUtils.OpenOrClone(workDir: workFolder, repoUrl: repo))
        {
            bool first = true;

            foreach (PackageUpdate package in packages)
            {
                logging.LogInformation($"* Updating {package.PackageId}...");
                PackageUpdateConfiguration config = BuildConfiguration(packageId: package.PackageId, exclude: package.Exclude);
                IReadOnlyList<PackageVersion> updatesMade = await packageUpdater.UpdateAsync(basePath: repository.Info.WorkingDirectory,
                                                                                             configuration: config,
                                                                                             packageSources: additionalSources,
                                                                                             cancellationToken: cancellationToken);

                Console.WriteLine($"Total updates: {updatesMade.Count}");

                if (updatesMade.Count != 0)
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
    }

    private static PackageUpdateConfiguration BuildConfiguration(string packageId, IReadOnlyList<string> exclude)
    {
        PackageMatch packageMatch = ExtractSearchPackage(packageId);
        Console.WriteLine($"Including {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");

        IReadOnlyList<PackageMatch> excludedPackages = GetExcludedPackages(exclude);

        return new(PackageMatch: packageMatch, ExcludedPackages: excludedPackages);
    }

    private static IReadOnlyList<PackageMatch> GetExcludedPackages(IReadOnlyList<string> excludes)
    {
        if (excludes.Count == 0)
        {
            return Array.Empty<PackageMatch>();
        }

        List<PackageMatch> excludedPackages = new();

        foreach (string exclude in excludes)
        {
            PackageMatch packageMatch = ExtractSearchPackage(exclude);

            excludedPackages.Add(packageMatch);

            Console.WriteLine($"Excluding {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");
        }

        return excludedPackages;
    }

    private static PackageMatch ExtractSearchPackage(string exclude)
    {
        string[] parts = exclude.Split(separator: ':');

        return parts.Length == 2
            ? new(parts[0], StringComparer.InvariantCultureIgnoreCase.Equals(parts[1], y: "prefix"))
            : new(parts[0], Prefix: false);
    }
}