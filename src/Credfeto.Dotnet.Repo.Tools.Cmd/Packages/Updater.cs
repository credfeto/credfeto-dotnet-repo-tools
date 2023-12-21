using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Dotnet.Repo.Git;
using Credfeto.Dotnet.Repo.Tools.Cmd.Build;
using Credfeto.Dotnet.Repo.Tools.Cmd.Exceptions;
using Credfeto.Package;
using FunFair.BuildCheck.Runner.Services;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

internal static class Updater
{
    private const string UPSTREAM = "origin";

    public static async Task UpdateRepositoriesAsync(UpdateContext updateContext,
                                                     IReadOnlyList<string> repos,
                                                     IReadOnlyList<PackageUpdate> packages,
                                                     IPackageUpdater packageUpdater,
                                                     IPackageCache packageCache,
                                                     ILogger logger,
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
                                                    IReadOnlyList<PackageUpdate> packages,
                                                    IPackageUpdater packageUpdater,
                                                    string repo,
                                                    ILogger logger,
                                                    CancellationToken cancellationToken)
    {
        logger.LogInformation($"Processing {repo}");

        using (Repository repository = await GitUtils.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            string? lastKnownGoodBuild = updateContext.TrackingCache.Get(repo);

            if (!HasDotNetFiles(repository: repository, out string? sourceDirectory, out IReadOnlyList<string>? solutions, out IReadOnlyList<string>? projects))
            {
                logger.LogInformation("No dotnet files found");
                updateContext.TrackingCache.Set(repoUrl: repo, value: repository.Head.Tip.Sha);

                return;
            }

            BuildSettings buildSettings = DotNetBuild.LoadBuildSettings(new ProjectLoader(), projects: projects);

            int totalUpdates = 0;

            foreach (PackageUpdate package in packages)
            {
                if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, y: repository.Head.Tip.Sha))
                {
                    await SolutionCheck.PreCheckAsync(solutions: solutions, logger: logger, cancellationToken: cancellationToken);

                    await DotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, logger: logger, cancellationToken: cancellationToken);

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

                    bool ok = await PostUpdateCheckAsync(solutions: solutions, logger: logger, cancellationToken: cancellationToken, sourceDirectory: sourceDirectory, buildSettings: buildSettings);

                    if (ok)
                    {
                        // TODO: commit changes, push update last known good build.
                        await CommitToRepositoryAsync(repo: repository, package: package, builtOk: ok, cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await GitUtils.ResetToMasterAsync(repo: repository, upstream: UPSTREAM, cancellationToken: cancellationToken);
                    }
                }
            }

            if (totalUpdates == 0)
            {
                // Attempt to create release
            }
        }
    }

    private static async ValueTask CommitToRepositoryAsync(Repository repo, PackageUpdate package, bool builtOk, CancellationToken cancellationToken)
    {
        await Task.Delay(millisecondsDelay: 1, cancellationToken: cancellationToken);
    }

    private static async Task<bool> PostUpdateCheckAsync(IReadOnlyList<string> solutions, string sourceDirectory, BuildSettings buildSettings, ILogger logger, CancellationToken cancellationToken)
    {
        bool ok = false;

        try
        {
            bool checkOk = await SolutionCheck.PostCheckAsync(solutions: solutions, logger: logger, cancellationToken: cancellationToken);

            if (checkOk)
            {
                await DotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, logger: logger, cancellationToken: cancellationToken);

                ok = true;
            }
        }
        catch (DotNetBuildErrorException exception)
        {
            logger.LogError(exception: exception, message: "Build failed");
            ok = false;
        }

        return ok;
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

    private static bool HasDotNetFiles(Repository repository,
                                       [NotNullWhen(true)] out string? sourceDirectory,
                                       [NotNullWhen(true)] out IReadOnlyList<string>? solutions,
                                       [NotNullWhen(true)] out IReadOnlyList<string>? projects)
    {
        string sourceFolder = Path.Combine(path1: repository.Info.WorkingDirectory, path2: "src");

        if (!Directory.Exists(sourceFolder))
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        string[] foundSolutions = Directory.GetFiles(path: sourceFolder, searchPattern: "*.sln", searchOption: SearchOption.AllDirectories);

        if (foundSolutions.Length == 0)
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        string[] foundProjects = Directory.GetFiles(path: sourceFolder, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);

        if (foundProjects.Length == 0)
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        sourceDirectory = sourceFolder;
        solutions = foundSolutions;
        projects = foundProjects;

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