using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.ChangeLog;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.CleanUp.Interfaces;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class BulkCodeCleanUp : IBulkCodeCleanUp
{
    private readonly IDotNetBuild _dotNetBuild;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly ILogger<BulkCodeCleanUp> _logger;
    private readonly IProjectXmlRewriter _projectXmlRewriter;
    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly ITrackingCache _trackingCache;

    private readonly IXmlDocCommentRemover _xmlDocCommentRemover;

    public BulkCodeCleanUp(ITrackingCache trackingCache,
                           IGitRepositoryFactory gitRepositoryFactory,
                           IGlobalJson globalJson,
                           IDotNetVersion dotNetVersion,
                           IReleaseConfigLoader releaseConfigLoader,
                           IProjectXmlRewriter projectXmlRewriter,
                           IXmlDocCommentRemover xmlDocCommentRemover,
                           IDotNetBuild dotNetBuild,
                           ILogger<BulkCodeCleanUp> logger)
    {
        this._trackingCache = trackingCache;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._globalJson = globalJson;
        this._dotNetVersion = dotNetVersion;
        this._releaseConfigLoader = releaseConfigLoader;
        this._projectXmlRewriter = projectXmlRewriter;
        this._xmlDocCommentRemover = xmlDocCommentRemover;
        this._dotNetBuild = dotNetBuild;
        this._logger = logger;
    }

    /*
     * Foreach repo
     *   ResetToMaster
     *   For each $solution
     *      IfExistsBranch cleanup/$solutionName
     *          continue
     *
     *      If !CodeBuilds
     *          continue
     *
     *      OK = RunCodeCleanup
     *
     *      If !OK
     *          CreateBranch cleanup/$solutionName/broken/$sha
     *          CommitAndPush
     *          ResetToMaster
     *      Else
     *          CreateBranch cleanup/$solutionName/clean/$sha
     *          CommitAndPush
     *          ResetToMaster
     *      End
     *
     */

    /*
     * RunCodeCleanup
     *
     *  If RemoveXmlDocsComments
     *     RemoveXmlDocComments
     *     if !CodeBuilds
     *       return false
     *
     *  Convert Resharper Suppression To SuppressMessage
     *     if !CodeBuilds
     *       return false
     *
     *  Foreach project in solution
     *    Project_cleanup (ordering)
     *
     *    if Changes && !CodeBuilds
     *     return false
     *
     *    Project_cleanup (jetbrains)
     *    if !CodeBuilds
     *      return false
     *
     *  Solution Cleanup (jetbrains)
     *    if !CodeBuilds
     *      return false
     *
     *  Return true
     */
    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        await this.LoadTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);

        using (IGitRepository templateRepo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: workFolder, repoUrl: templateRepository, cancellationToken: cancellationToken))
        {
            CleanupUpdateContext updateContext = await this.BuildUpdateContextAsync(templateRepo: templateRepo,
                                                                                    workFolder: workFolder,
                                                                                    trackingFileName: trackingFileName,
                                                                                    releaseConfigFileName: releaseConfigFileName,
                                                                                    cancellationToken: cancellationToken);

            try
            {
                await this.UpdateRepositoriesAsync(updateContext: updateContext, repositories: repositories, cancellationToken: cancellationToken);
            }
            finally
            {
                await this.SaveTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask UpdateRepositoriesAsync(CleanupUpdateContext updateContext, IReadOnlyList<string> repositories, CancellationToken cancellationToken)
    {
        try
        {
            foreach (string repo in repositories)
            {
                try
                {
                    await this.UpdateRepositoryAsync(updateContext: updateContext, repo: repo, cancellationToken: cancellationToken);
                }
                catch (SolutionCheckFailedException exception)
                {
                    this._logger.LogSolutionCheckFailed(exception: exception);
                }
                catch (DotNetBuildErrorException exception)
                {
                    this._logger.LogBuildFailedOnRepoCheck(exception: exception);
                }
                finally
                {
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

    private async ValueTask UpdateRepositoryAsync(CleanupUpdateContext updateContext, string repo, CancellationToken cancellationToken)
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

            await this.ProcessRepoUpdatesAsync(updateContext: updateContext, repoContext: repoContext, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask ProcessRepoUpdatesAsync(CleanupUpdateContext updateContext, RepoContext repoContext, CancellationToken cancellationToken)
    {
        if (repoContext.HasDotNetSolutions(out string? sourceDirectory, out IReadOnlyList<string>? _))
        {
            IReadOnlyList<string> projects = Directory.GetFiles(path: sourceDirectory, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);
            BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(projects: projects, cancellationToken: cancellationToken);

            await this.ReOrderProjectFilesAsync(repoContext: repoContext, sourceDirectory: sourceDirectory, projects: projects, buildSettings: buildSettings, cancellationToken: cancellationToken);
            await this.RemoveXmlDocCommentsAsync(repoContext: repoContext, sourceDirectory: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogNoDotNetFilesFound();
        }

        await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: repoContext.Repository.HeadRev, cancellationToken: cancellationToken);
    }

    private async ValueTask RemoveXmlDocCommentsAsync(RepoContext repoContext, string sourceDirectory, BuildSettings buildSettings, CancellationToken cancellationToken)
    {
        BuildOverride buildOverride = new(PreRelease: true);

        IReadOnlyList<string> sourceFiles = Directory.GetFiles(path: sourceDirectory, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories);

        if (sourceFiles is not [])
        {
            return;
        }

        await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

        bool lastBuildFailed = false;

        foreach (string sourceFile in sourceFiles)
        {
            if (lastBuildFailed)
            {
                try
                {
                    await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);
                    lastBuildFailed = false;
                }
                catch (DotNetBuildErrorException)
                {
                    await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

                    throw;
                }
            }

            string content = await File.ReadAllTextAsync(path: sourceFile, encoding: Encoding.UTF8, cancellationToken: cancellationToken);
            string cleanedContent = this._xmlDocCommentRemover.RemoveXmlDocComments(content);

            if (StringComparer.InvariantCultureIgnoreCase.Equals(x: content, y: cleanedContent))
            {
                // no changes
                continue;
            }

            string sourceFileName = Path.GetFileName(sourceFile);

            await File.WriteAllTextAsync(path: sourceFile, contents: cleanedContent, cancellationToken: cancellationToken);

            try
            {
                await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

                lastBuildFailed = false;
                await repoContext.Repository.CommitAsync($"Cleanup: {sourceFileName}", cancellationToken: cancellationToken);
                await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);
            }
            catch (DotNetBuildErrorException exception)
            {
                this._logger.LogBuildFailedOnRepoCheck(exception: exception);

                await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
                lastBuildFailed = true;
            }
        }
    }

    private async ValueTask ReOrderProjectFilesAsync(RepoContext repoContext, string sourceDirectory, IReadOnlyList<string> projects, BuildSettings buildSettings, CancellationToken cancellationToken)
    {
        if (projects is not [])
        {
            return;
        }

        BuildOverride buildOverride = new(PreRelease: true);

        await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

        bool lastBuildFailed = false;

        foreach (string project in projects)
        {
            if (lastBuildFailed)
            {
                try
                {
                    await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);
                }
                catch (DotNetBuildErrorException)
                {
                    await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);

                    throw;
                }
            }

            XmlDocument doc = await LoadProjectAsync(path: project, cancellationToken: cancellationToken);

            string projectName = Path.GetFileName(project);

            this.ProjectCleanup(project: doc, projectFile: project);

            await SaveProjectAsync(project: project, doc: doc);

            try
            {
                await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, buildOverride: buildOverride, cancellationToken: cancellationToken);

                lastBuildFailed = false;
                await repoContext.Repository.CommitAsync($"Cleanup: {projectName}", cancellationToken: cancellationToken);
                await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);
            }
            catch (DotNetBuildErrorException exception)
            {
                this._logger.LogBuildFailedOnRepoCheck(exception: exception);

                await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
                lastBuildFailed = true;
            }
        }
    }

    private void ProjectCleanup(XmlDocument project, string projectFile)
    {
        this._projectXmlRewriter.ReOrderPropertyGroups(projectDocument: project, filename: projectFile);
        this._projectXmlRewriter.ReOrderIncludes(projectDocument: project, filename: projectFile);
    }

    private static async ValueTask SaveProjectAsync(string project, XmlDocument doc)
    {
        XmlWriterSettings settings = new()
                                     {
                                         Indent = true,
                                         IndentChars = "  ",
                                         NewLineOnAttributes = false,
                                         OmitXmlDeclaration = true,
                                         Async = true
                                     };

        await using (XmlWriter xmlWriter = XmlWriter.Create(outputFileName: project, settings: settings))
        {
            doc.Save(xmlWriter);
            await ValueTask.CompletedTask;
        }
    }

    private static async ValueTask<XmlDocument> LoadProjectAsync(string path, CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(path: path, encoding: Encoding.UTF8, cancellationToken: cancellationToken);
        XmlDocument doc = new();

        doc.LoadXml(content);

        return doc;
    }

    private async ValueTask<CleanupUpdateContext> BuildUpdateContextAsync(IGitRepository templateRepo,
                                                                          string workFolder,
                                                                          string trackingFileName,
                                                                          string releaseConfigFileName,
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

        return new(WorkFolder: workFolder, TrackingFileName: trackingFileName, DotNetSettings: dotNetSettings, ReleaseConfig: releaseConfig);
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

    private ValueTask SaveTrackingCacheAsync(string? trackingFile, in CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(trackingFile)
            ? ValueTask.CompletedTask
            : this._trackingCache.SaveAsync(fileName: trackingFile, cancellationToken: cancellationToken);
    }
}