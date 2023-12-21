using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Package;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

internal static class Updater
{
    private const string UPSTREAM = "origin";

    public static async Task UpdateRepositoriesAsync(UpdateContext updateContext,
                                                     IReadOnlyList<string> repos,
                                                     IDiagnosticLogger logger,
                                                     IReadOnlyList<PackageUpdate> packages,
                                                     IPackageUpdater packageUpdater,
                                                     IPackageCache packageCache,
                                                     CancellationToken cancellationToken)
    {
        foreach (string repo in repos)
        {
            try
            {
                await UpdateRepositoryAsync(updateContext: updateContext, logger: logger, packages: packages, packageUpdater: packageUpdater, cancellationToken: cancellationToken, repo: repo);
            }
            catch (SolutionCheckFailedException exception)
            {
                logger.LogError(exception: exception, message: "Solution check failed");
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
                                                    IDiagnosticLogger logger,
                                                    IReadOnlyList<PackageUpdate> packages,
                                                    IPackageUpdater packageUpdater,
                                                    string repo,
                                                    CancellationToken cancellationToken)
    {
        logger.LogInformation($"Processing {repo}");

        using (Repository repository = await GitUtils.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            string? lastKnownGoodBuild = updateContext.TrackingCache.Get(repo);

            if (!HasDotNetFiles(repository: repository, out IReadOnlyList<string>? solutions))
            {
                logger.LogInformation("No dotnet files found");
                updateContext.TrackingCache.Set(repoUrl: repo, value: repository.Head.Tip.Sha);

                return;
            }

            int totalUpdates = 0;

            foreach (PackageUpdate package in packages)
            {
                if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, y: repository.Head.Tip.Sha))
                {
                    await SolutionCheck.PreCheckAsync(solutions: solutions, logging: logger, cancellationToken: cancellationToken);

                    lastKnownGoodBuild = repository.Head.Tip.Sha;
                    updateContext.TrackingCache.Set(repoUrl: repo, value: lastKnownGoodBuild);
                }

                IReadOnlyList<PackageVersion> updatesMade = await UpdatePackagesAsync(updateContext: updateContext,
                                                                                      packageUpdater: packageUpdater,
                                                                                      package: package,
                                                                                      repository: repository,
                                                                                      logger: logger,
                                                                                      cancellationToken: cancellationToken);

                if (updatesMade.Count != 0)
                {
                    ++totalUpdates;

                    bool checkOk = await SolutionCheck.PostCheckAsync(solutions: solutions, logging: logger, cancellationToken: cancellationToken);

                    if (checkOk)
                    {
                        // TODO: commit changes, push update last known good build.
                    }

                    await GitUtils.ResetToMasterAsync(repo: repository, upstream: UPSTREAM, cancellationToken: cancellationToken);
                }
            }

            if (totalUpdates == 0)
            {
                // Attempt to create release
            }
        }
    }

    private static async ValueTask<IReadOnlyList<PackageVersion>> UpdatePackagesAsync(UpdateContext updateContext,
                                                                                      IPackageUpdater packageUpdater,
                                                                                      PackageUpdate package,
                                                                                      Repository repository,
                                                                                      ILogger logger,
                                                                                      CancellationToken cancellationToken)
    {
        logger.LogInformation($"* Updating {package.PackageId}...");
        PackageUpdateConfiguration config = BuildConfiguration(package);

        return await packageUpdater.UpdateAsync(basePath: repository.Info.WorkingDirectory,
                                                configuration: config,
                                                packageSources: updateContext.AdditionalSources,
                                                cancellationToken: cancellationToken);
    }

    private static bool HasDotNetFiles(Repository repository, [NotNullWhen(true)] out IReadOnlyList<string>? solutions)
    {
        string[] foundSolutions = Directory.GetFiles(path: repository.Info.WorkingDirectory, searchPattern: "*.sln", searchOption: SearchOption.AllDirectories);

        if (foundSolutions.Length == 0)
        {
            solutions = null;

            return false;
        }

        string[] foundProjects = Directory.GetFiles(path: repository.Info.WorkingDirectory, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);

        if (foundProjects.Length == 0)
        {
            solutions = null;

            return false;
        }

        solutions = foundSolutions;

        return true;
    }

    private static PackageUpdateConfiguration BuildConfiguration(PackageUpdate package)
    {
        PackageMatch packageMatch = new(PackageId: package.PackageId, Prefix: !package.ExactMatch);
        Console.WriteLine($"Including {packageMatch.PackageId} (Using Prefix match: {packageMatch.Prefix})");

        IReadOnlyList<PackageMatch> excludedPackages = GetExcludedPackages(package.Exclude ?? Array.Empty<PackageExclude>());

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