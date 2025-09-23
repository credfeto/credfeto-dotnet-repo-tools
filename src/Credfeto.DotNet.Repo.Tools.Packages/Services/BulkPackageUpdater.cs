using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.ChangeLog;
using Credfeto.ChangeLog.Exceptions;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Packages.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Credfeto.Package;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services;

public sealed class BulkPackageUpdater : IBulkPackageUpdater
{
    private readonly IBulkPackageConfigLoader _bulkPackageConfigLoader;
    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly ILogger<BulkPackageUpdater> _logger;
    private readonly IPackageCache _packageCache;
    private readonly IPackageUpdateConfigurationBuilder _packageUpdateConfigurationBuilder;
    private readonly IPackageUpdater _packageUpdater;
    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly IReleaseGeneration _releaseGeneration;

    private readonly ISinglePackageUpdater _singlePackageUpdater;
    private readonly ITrackingCache _trackingCache;

    public BulkPackageUpdater(IPackageUpdater packageUpdater,
                              IPackageCache packageCache,
                              ITrackingCache trackingCache,
                              IGlobalJson globalJson,
                              IDotNetVersion dotNetVersion,
                              IDotNetBuild dotNetBuild,
                              IDotNetFilesDetector dotNetFilesDetector,
                              IReleaseConfigLoader releaseConfigLoader,
                              IReleaseGeneration releaseGeneration,
                              IGitRepositoryFactory gitRepositoryFactory,
                              IBulkPackageConfigLoader bulkPackageConfigLoader,
                              ISinglePackageUpdater singlePackageUpdater,
                              IPackageUpdateConfigurationBuilder packageUpdateConfigurationBuilder,
                              ILogger<BulkPackageUpdater> logger)
    {
        this._packageUpdater = packageUpdater;
        this._packageCache = packageCache;
        this._trackingCache = trackingCache;
        this._globalJson = globalJson;
        this._dotNetVersion = dotNetVersion;
        this._dotNetBuild = dotNetBuild;
        this._dotNetFilesDetector = dotNetFilesDetector;
        this._releaseConfigLoader = releaseConfigLoader;
        this._releaseGeneration = releaseGeneration;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._bulkPackageConfigLoader = bulkPackageConfigLoader;
        this._singlePackageUpdater = singlePackageUpdater;
        this._packageUpdateConfigurationBuilder = packageUpdateConfigurationBuilder;
        this._logger = logger;
    }

    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string? cacheFileName,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> additionalNugetSources,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        await this.LoadPackageCacheAsync(packageCacheFile: cacheFileName, cancellationToken: cancellationToken);
        await this.LoadTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);

        IReadOnlyList<PackageUpdate> packages = await this._bulkPackageConfigLoader.LoadAsync(path: packagesFileName, cancellationToken: cancellationToken);

        using (IGitRepository templateRepo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: workFolder, repoUrl: templateRepository, cancellationToken: cancellationToken))
        {
            PackageUpdateContext updateContext = await this.BuildUpdateContextAsync(cacheFileName: cacheFileName,
                                                                                    templateRepo: templateRepo,
                                                                                    workFolder: workFolder,
                                                                                    trackingFileName: trackingFileName,
                                                                                    releaseConfigFileName: releaseConfigFileName,
                                                                                    additionalNugetSources: additionalNugetSources,
                                                                                    cancellationToken: cancellationToken);

            await this.UpdateCachedPackagesAsync(workFolder: workFolder, packages: packages, updateContext: updateContext, cancellationToken: cancellationToken);

            await this.UpdateRepositoriesAndTrackingAsync(updateContext: updateContext,
                                                          repositories: repositories,
                                                          packages: packages,
                                                          cacheFileName: cacheFileName,
                                                          trackingFileName: trackingFileName,
                                                          cancellationToken: cancellationToken);
        }
    }

    private async ValueTask UpdateRepositoriesAndTrackingAsync(PackageUpdateContext updateContext,
                                                               IReadOnlyList<string> repositories,
                                                               IReadOnlyList<PackageUpdate> packages,
                                                               string? cacheFileName,
                                                               string trackingFileName,
                                                               CancellationToken cancellationToken)
    {
        try
        {
            await this.UpdateRepositoriesAsync(updateContext: updateContext, repositories: repositories, packages: packages, cancellationToken: cancellationToken);
        }
        finally
        {
            await this.SavePackageCacheAsync(packageCacheFile: cacheFileName, cancellationToken: cancellationToken);
            await this.SaveTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);
        }
    }

    public async ValueTask UpdateRepositoriesAsync(PackageUpdateContext updateContext, IReadOnlyList<string> repositories, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        try
        {
            foreach (string repo in repositories)
            {
                try
                {
                    await this.UpdateRepositoryAsync(updateContext: updateContext, packages: packages, repo: repo, cancellationToken: cancellationToken);
                }
                catch (SolutionCheckFailedException exception)
                {
                    this._logger.LogSolutionCheckFailed(exception: exception);
                }
                catch (DotNetBuildErrorException exception)
                {
                    this._logger.LogBuildFailedOnRepoCheck(exception: exception);
                }
                catch (ReleaseTooOldException exception)
                {
                    this._logger.LogBuildFailedOnCreateRelease(message: exception.Message, exception: exception);
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(updateContext.CacheFileName))
                    {
                        await this._packageCache.SaveAsync(fileName: updateContext.CacheFileName, cancellationToken: cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(updateContext.TrackingFileName))
                    {
                        await this._trackingCache.SaveAsync(fileName: updateContext.TrackingFileName, cancellationToken: cancellationToken);
                    }
                }
            }
        }
        catch (ReleaseCreatedException exception)
        {
            this._logger.LogReleaseCreatedAbortingRun(exception: exception);
            this._logger.LogReleaseCreated(message: exception.Message, exception: exception);
        }
    }

    private async ValueTask UpdateCachedPackagesAsync(string workFolder, IReadOnlyList<PackageUpdate> packages, PackageUpdateContext updateContext, CancellationToken cancellationToken)
    {
        IReadOnlyList<PackageVersion> allPackages = this._packageCache.GetAll();

        if (allPackages.Count == 0)
        {
            // no cached packages
            return;
        }

        string packagesFolder = Path.Combine(path1: workFolder, path2: "_Packages");

        if (!Directory.Exists(packagesFolder))
        {
            Directory.CreateDirectory(packagesFolder);
        }

        XmlDocument document = BuildReferencedPackagesXmlDocument(allPackages);

        string projectFileName = Path.Combine(path1: packagesFolder, path2: "Packages.csproj");
        document.Save(projectFileName);

        // Clear the cached packages as about to use the previously cached list to build a new set of versions
        // This is to ensure that the latest versions are used when running against the real list of repos.
        this._packageCache.Reset();

        int updates = 0;
        this._logger.LogPreLoadingCachedPackages();

        foreach (PackageUpdate package in packages)
        {
            this._logger.LogUpdatingCachedPackage(package.PackageId);
            PackageUpdateConfiguration config = this._packageUpdateConfigurationBuilder.Build(package);

            IReadOnlyList<PackageVersion> updated = await this._packageUpdater.UpdateAsync(
                basePath: packagesFolder,
                configuration: config,
                packageSources: updateContext.AdditionalSources,
                cancellationToken: cancellationToken);

            this._logger.LogUpdatedCachedPackages(packageId: package.PackageId, count: updated.Count);
            updates += updated.Count;
        }

        this._logger.LogUpdatedCachedPackagesTotal(updates);
    }

    private static XmlDocument BuildReferencedPackagesXmlDocument(IReadOnlyList<PackageVersion> allPackages)
    {
        XmlDocument document = new();
        XmlElement projectElement = document.CreateElement("Project");
        projectElement.SetAttribute(name: "Sdk", value: "Microsoft.NET.Sdk");
        document.AppendChild(projectElement);

        XmlElement itemGroup = document.CreateElement("ItemGroup");

        foreach (PackageVersion package in allPackages)
        {
            XmlElement packageReference = document.CreateElement("PackageReference");
            packageReference.SetAttribute(name: "Include", value: package.PackageId);
            packageReference.SetAttribute(name: "Version", package.Version.ToString());
            itemGroup.AppendChild(packageReference);
        }

        projectElement.AppendChild(itemGroup);

        return document;
    }

    private async ValueTask UpdateRepositoryAsync(PackageUpdateContext updateContext, IReadOnlyList<PackageUpdate> packages, string repo, CancellationToken cancellationToken)
    {
        this._logger.LogProcessingRepo(repo);

        using (IGitRepository repository = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: updateContext.WorkFolder, repoUrl: repo, cancellationToken: cancellationToken))
        {
            if (!ChangeLogDetector.TryFindChangeLog(repository: repository.Active, out string? changeLogFileName))
            {
                this._logger.LogNoChangelogFound();
                await this._trackingCache.UpdateTrackingAsync(new(Repository: repository, ChangeLogFileName: "?"),
                                                              updateContext: updateContext,
                                                              value: repository.HeadRev,
                                                              cancellationToken: cancellationToken);

                return;
            }

            RepoContext repoContext = new(Repository: repository, ChangeLogFileName: changeLogFileName);

            await this.ProcessRepoUpdatesAsync(updateContext: updateContext, repoContext: repoContext, packages: packages, cancellationToken: cancellationToken);
        }
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Needs Review")]
    private async ValueTask ProcessRepoUpdatesAsync(PackageUpdateContext updateContext, RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        DotNetFiles? dotNetFiles = await this._dotNetFilesDetector.FindAsync(baseFolder: repoContext.WorkingDirectory, cancellationToken: cancellationToken);

        if (dotNetFiles is null)
        {
            this._logger.LogNoDotNetFilesFound();
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: repoContext.Repository.HeadRev, cancellationToken: cancellationToken);

            return;
        }

        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(projects: dotNetFiles.Value.Projects, cancellationToken: cancellationToken);

        int totalUpdates = 0;

        foreach (PackageUpdate package in packages)
        {
            bool updated = await this._singlePackageUpdater.UpdateAsync(updateContext: updateContext,
                                                                        repoContext: repoContext,
                                                                        solutions: dotNetFiles.Value.Solutions,
                                                                        sourceDirectory: dotNetFiles.Value.SourceDirectory,
                                                                        buildSettings: buildSettings,
                                                                        dotNetSettings: updateContext.DotNetSettings,
                                                                        package: package,
                                                                        cancellationToken: cancellationToken);

            if (updated)
            {
                ++totalUpdates;
            }
        }

        if (totalUpdates == 0)
        {
            // no updates in this run - so might be able to create a release
            await this._releaseGeneration.TryCreateNextPatchAsync(repoContext: repoContext,
                                                                  basePath: dotNetFiles.Value.SourceDirectory,
                                                                  buildSettings: buildSettings,
                                                                  dotNetSettings: updateContext.DotNetSettings,
                                                                  solutions: dotNetFiles.Value.Solutions,
                                                                  packages: packages,
                                                                  releaseConfig: updateContext.ReleaseConfig,
                                                                  cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<PackageUpdateContext> BuildUpdateContextAsync(string? cacheFileName,
                                                                          IGitRepository templateRepo,
                                                                          string workFolder,
                                                                          string trackingFileName,
                                                                          string releaseConfigFileName,
                                                                          IReadOnlyList<string> additionalNugetSources,
                                                                          CancellationToken cancellationToken)
    {
        DotNetVersionSettings dotNetSettings = await this._globalJson.LoadGlobalJsonAsync(baseFolder: templateRepo.WorkingDirectory, cancellationToken: cancellationToken);

        IReadOnlyList<Version> installedDotNetSdks = await this._dotNetVersion.GetInstalledSdksAsync(cancellationToken);

        if (dotNetSettings.SdkVersion is not null && Version.TryParse(input: dotNetSettings.SdkVersion, out Version? sdkVersion))
        {
            if (!installedDotNetSdks.Contains(sdkVersion))
            {
                this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedDotNetSdks);

                throw new DotNetBuildErrorException("SDK version specified in global.json is not installed");
            }
        }

        ReleaseConfig releaseConfig = await this._releaseConfigLoader.LoadAsync(path: releaseConfigFileName, cancellationToken: cancellationToken);

        return new(WorkFolder: workFolder,
                   CacheFileName: cacheFileName,
                   TrackingFileName: trackingFileName,
                   AdditionalSources: additionalNugetSources,
                   DotNetSettings: dotNetSettings,
                   ReleaseConfig: releaseConfig);
    }

    private ValueTask SaveTrackingCacheAsync(string? trackingFile, in CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(trackingFile)
            ? ValueTask.CompletedTask
            : this._trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }

    private async ValueTask SavePackageCacheAsync(string? packageCacheFile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageCacheFile))
        {
            return;
        }

        await this._packageCache.SaveAsync(fileName: packageCacheFile, cancellationToken: cancellationToken);
    }

    private ValueTask LoadTrackingCacheAsync(string? trackingFile, in CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingFile))
        {
            return ValueTask.CompletedTask;
        }

        if (!File.Exists(trackingFile))
        {
            return ValueTask.CompletedTask;
        }

        return this._trackingCache.LoadAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }

    private async ValueTask LoadPackageCacheAsync(string? packageCacheFile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageCacheFile))
        {
            return;
        }

        if (!File.Exists(packageCacheFile))
        {
            return;
        }

        await this._packageCache.LoadAsync(fileName: packageCacheFile, cancellationToken: cancellationToken);
    }
}