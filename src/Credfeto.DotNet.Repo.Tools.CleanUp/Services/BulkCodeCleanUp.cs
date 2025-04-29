using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Credfeto.ChangeLog;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.CleanUp.Helpers;
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
    private readonly ISourceFileReformatter _sourceFileReformatter;
    private readonly ITrackingCache _trackingCache;

    private readonly IXmlDocCommentRemover _xmlDocCommentRemover;

    public BulkCodeCleanUp(
        ITrackingCache trackingCache,
        IGitRepositoryFactory gitRepositoryFactory,
        IGlobalJson globalJson,
        IDotNetVersion dotNetVersion,
        IReleaseConfigLoader releaseConfigLoader,
        IProjectXmlRewriter projectXmlRewriter,
        ISourceFileReformatter sourceFileReformatter,
        IXmlDocCommentRemover xmlDocCommentRemover,
        IDotNetBuild dotNetBuild,
        ILogger<BulkCodeCleanUp> logger
    )
    {
        this._trackingCache = trackingCache;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._globalJson = globalJson;
        this._dotNetVersion = dotNetVersion;
        this._releaseConfigLoader = releaseConfigLoader;
        this._projectXmlRewriter = projectXmlRewriter;
        this._sourceFileReformatter = sourceFileReformatter;
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
    public async ValueTask BulkUpdateAsync(
        string templateRepository,
        string trackingFileName,
        string packagesFileName,
        string workFolder,
        string releaseConfigFileName,
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    )
    {
        await this.LoadTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);

        using (
            IGitRepository templateRepo = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: workFolder,
                repoUrl: templateRepository,
                cancellationToken: cancellationToken
            )
        )
        {
            CleanupUpdateContext updateContext = await this.BuildUpdateContextAsync(
                templateRepo: templateRepo,
                workFolder: workFolder,
                trackingFileName: trackingFileName,
                releaseConfigFileName: releaseConfigFileName,
                cancellationToken: cancellationToken
            );

            try
            {
                await this.UpdateRepositoriesAsync(
                    updateContext: updateContext,
                    repositories: repositories,
                    cancellationToken: cancellationToken
                );
            }
            finally
            {
                await this.SaveTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask UpdateRepositoriesAsync(
        CleanupUpdateContext updateContext,
        IReadOnlyList<string> repositories,
        CancellationToken cancellationToken
    )
    {
        try
        {
            foreach (string repo in repositories)
            {
                try
                {
                    await this.UpdateRepositoryAsync(
                        updateContext: updateContext,
                        repo: repo,
                        cancellationToken: cancellationToken
                    );
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
                        await this._trackingCache.SaveAsync(
                            fileName: updateContext.TrackingFileName,
                            cancellationToken: cancellationToken
                        );
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

    private async ValueTask UpdateRepositoryAsync(
        CleanupUpdateContext updateContext,
        string repo,
        CancellationToken cancellationToken
    )
    {
        this._logger.LogProcessingRepo(repo);

        using (
            IGitRepository repository = await this._gitRepositoryFactory.OpenOrCloneAsync(
                workDir: updateContext.WorkFolder,
                repoUrl: repo,
                cancellationToken: cancellationToken
            )
        )
        {
            if (!ChangeLogDetector.TryFindChangeLog(repository: repository.Active, out string? changeLogFileName))
            {
                this._logger.LogNoChangelogFound();
                await this._trackingCache.UpdateTrackingAsync(
                    new(Repository: repository, ChangeLogFileName: "?"),
                    updateContext: updateContext,
                    value: repository.HeadRev,
                    cancellationToken: cancellationToken
                );

                return;
            }

            RepoContext repoContext = new(Repository: repository, ChangeLogFileName: changeLogFileName);

            await this.ProcessRepoUpdatesAsync(
                updateContext: updateContext,
                repoContext: repoContext,
                cancellationToken: cancellationToken
            );
        }
    }

    private async ValueTask ProcessRepoUpdatesAsync(
        CleanupUpdateContext updateContext,
        RepoContext repoContext,
        CancellationToken cancellationToken
    )
    {
        if (repoContext.HasDotNetSolutions(out string? sourceDirectory, out IReadOnlyList<string>? _))
        {
            IReadOnlyList<string> projects = Directory.GetFiles(
                path: sourceDirectory,
                searchPattern: "*.csproj",
                searchOption: SearchOption.AllDirectories
            );
            BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(
                projects: projects,
                cancellationToken: cancellationToken
            );

            await this.ReOrderProjectFilesAsync(
                repoContext: repoContext,
                sourceDirectory: sourceDirectory,
                projects: projects,
                buildSettings: buildSettings,
                cancellationToken: cancellationToken
            );
            await this.CleanupSourceAsync(
                repoContext: repoContext,
                sourceDirectory: sourceDirectory,
                buildSettings: buildSettings,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            this._logger.LogNoDotNetFilesFound();
        }

        await this._trackingCache.UpdateTrackingAsync(
            repoContext: repoContext,
            updateContext: updateContext,
            value: repoContext.Repository.HeadRev,
            cancellationToken: cancellationToken
        );
    }

    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Needs Review"
    )]
    private async ValueTask FileCleanupAsync(
        RepoContext repoContext,
        string sourceDirectory,
        BuildSettings buildSettings,
        IReadOnlyList<string> sourceFiles,
        Func<string, ValueTask<bool>> cleaner,
        CancellationToken cancellationToken
    )
    {
        if (sourceFiles is [])
        {
            return;
        }

        BuildOverride buildOverride = new(PreRelease: true);

        await this._dotNetBuild.BuildAsync(
            basePath: sourceDirectory,
            buildSettings: buildSettings,
            buildOverride: buildOverride,
            cancellationToken: cancellationToken
        );

        bool lastBuildFailed = false;

        foreach (string sourceFile in sourceFiles)
        {
            if (lastBuildFailed)
            {
                try
                {
                    await this._dotNetBuild.BuildAsync(
                        basePath: sourceDirectory,
                        buildSettings: buildSettings,
                        buildOverride: buildOverride,
                        cancellationToken: cancellationToken
                    );
                    lastBuildFailed = false;
                }
                catch (DotNetBuildErrorException)
                {
                    await repoContext.Repository.ResetToMasterAsync(
                        upstream: GitConstants.Upstream,
                        cancellationToken: cancellationToken
                    );

                    throw;
                }
            }

            this._logger.CleaningFile(sourceFile);

            bool changed = await cleaner(sourceFile);

            if (!changed)
            {
                this._logger.CleaningFileUnchanged(sourceFile);

                continue;
            }

            this._logger.CleaningFileDifferent(sourceFile);

            string sourceFileName = Path.GetFileName(sourceFile);

            lastBuildFailed = await this.TestBuildAndCommitIfCleanAsync(
                repoContext: repoContext,
                sourceDirectory: sourceDirectory,
                buildSettings: buildSettings,
                buildOverride: buildOverride,
                sourceFileName: sourceFileName,
                cancellationToken: cancellationToken
            );
        }
    }

    private async ValueTask<bool> TestBuildAndCommitIfCleanAsync(
        RepoContext repoContext,
        string sourceDirectory,
        BuildSettings buildSettings,
        BuildOverride buildOverride,
        string sourceFileName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await this._dotNetBuild.BuildAsync(
                basePath: sourceDirectory,
                buildSettings: buildSettings,
                buildOverride: buildOverride,
                cancellationToken: cancellationToken
            );

            await repoContext.Repository.CommitAsync(
                $"Cleanup: {sourceFileName}",
                cancellationToken: cancellationToken
            );
            await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);

            return false;
        }
        catch (DotNetBuildErrorException exception)
        {
            this._logger.LogBuildFailedOnRepoCheck(exception: exception);

            await repoContext.Repository.ResetToMasterAsync(
                upstream: GitConstants.Upstream,
                cancellationToken: cancellationToken
            );

            return true;
        }
    }

    private async ValueTask CleanupSourceAsync(
        RepoContext repoContext,
        string sourceDirectory,
        BuildSettings buildSettings,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> sourceFiles = SourceFilesExcludingGenerated(sourceDirectory);

        await this.RemoveXmlDocCommentsAsync(
            repoContext: repoContext,
            sourceDirectory: sourceDirectory,
            sourceFiles: sourceFiles,
            buildSettings: buildSettings,
            cancellationToken: cancellationToken
        );
        await this.ReformatCSharpAsync(
            repoContext: repoContext,
            sourceDirectory: sourceDirectory,
            sourceFiles: sourceFiles,
            buildSettings: buildSettings,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask RemoveXmlDocCommentsAsync(
        RepoContext repoContext,
        string sourceDirectory,
        IReadOnlyList<string> sourceFiles,
        BuildSettings buildSettings,
        CancellationToken cancellationToken
    )
    {
        await this.FileCleanupAsync(
            repoContext: repoContext,
            sourceDirectory: sourceDirectory,
            buildSettings: buildSettings,
            sourceFiles: sourceFiles,
            cleaner: DoCleanupAsync,
            cancellationToken: cancellationToken
        );

        async ValueTask<bool> DoCleanupAsync(string fileName)
        {
            string content = await File.ReadAllTextAsync(
                path: fileName,
                encoding: Encoding.UTF8,
                cancellationToken: cancellationToken
            );
            string cleanedContent = this._xmlDocCommentRemover.RemoveXmlDocComments(content);

            if (StringComparer.InvariantCultureIgnoreCase.Equals(x: content, y: cleanedContent))
            {
                // no changes
                return false;
            }

            await File.WriteAllTextAsync(
                path: fileName,
                contents: cleanedContent,
                cancellationToken: cancellationToken
            );

            return true;
        }
    }

    private async ValueTask ReformatCSharpAsync(
        RepoContext repoContext,
        string sourceDirectory,
        IReadOnlyList<string> sourceFiles,
        BuildSettings buildSettings,
        CancellationToken cancellationToken
    )
    {
        await this.FileCleanupAsync(
            repoContext: repoContext,
            sourceDirectory: sourceDirectory,
            buildSettings: buildSettings,
            sourceFiles: sourceFiles,
            cleaner: DoCleanupAsync,
            cancellationToken: cancellationToken
        );

        async ValueTask<bool> DoCleanupAsync(string fileName)
        {
            string content = await File.ReadAllTextAsync(
                path: fileName,
                encoding: Encoding.UTF8,
                cancellationToken: cancellationToken
            );
            string cleanedContent = await this._sourceFileReformatter.ReformatAsync(
                content: content,
                fileName: fileName,
                cancellationToken: cancellationToken
            );

            if (StringComparer.InvariantCultureIgnoreCase.Equals(x: content, y: cleanedContent))
            {
                // no changes
                return false;
            }

            await File.WriteAllTextAsync(
                path: fileName,
                contents: cleanedContent,
                cancellationToken: cancellationToken
            );

            return true;
        }
    }

    private static IReadOnlyList<string> SourceFilesExcludingGenerated(string sourceDirectory)
    {
        return
        [
            .. Directory
                .GetFiles(path: sourceDirectory, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories)
                .Where(IsNonGenerated),
        ];

        bool IsNonGenerated(string filename)
        {
            return GeneratedSource.IsNonGenerated(filename.Substring(sourceDirectory.Length));
        }
    }

    private async ValueTask ReOrderProjectFilesAsync(
        RepoContext repoContext,
        string sourceDirectory,
        IReadOnlyList<string> projects,
        BuildSettings buildSettings,
        CancellationToken cancellationToken
    )
    {
        if (projects is [])
        {
            return;
        }

        await this.FileCleanupAsync(
            repoContext: repoContext,
            sourceDirectory: sourceDirectory,
            buildSettings: buildSettings,
            sourceFiles: projects,
            cleaner: DoCleanupAsync,
            cancellationToken: cancellationToken
        );

        async ValueTask<bool> DoCleanupAsync(string project)
        {
            (XmlDocument doc, string content) = await LoadProjectAsync(
                path: project,
                cancellationToken: cancellationToken
            );

            string projectName = Path.GetFileName(project);

            if (!this.ProjectCleanup(project: doc, projectFile: projectName))
            {
                return false;
            }

            await SaveProjectAsync(project: project, doc: doc);

            string contentAfter = await LoadProjectTextAsync(path: project, cancellationToken: cancellationToken);

            return !StringComparer.Ordinal.Equals(x: contentAfter, y: content);
        }
    }

    private bool ProjectCleanup(XmlDocument project, string projectFile)
    {
        this._logger.StartingProjectCleaup(projectFile);

        int changes = 0;

        try
        {
            if (this._projectXmlRewriter.ReOrderPropertyGroups(projectDocument: project, filename: projectFile))
            {
                ++changes;
            }

            if (this._projectXmlRewriter.ReOrderIncludes(projectDocument: project, filename: projectFile))
            {
                ++changes;
            }
        }
        catch (Exception exception)
        {
            this._logger.FailedProjectCleanup(filename: projectFile, message: exception.Message, exception: exception);

            return false;
        }

        this._logger.CompletingProjectCleanup(filename: projectFile, changes: changes);

        return changes != 0;
    }

    private static async ValueTask SaveProjectAsync(string project, XmlDocument doc)
    {
        XmlWriterSettings settings = new()
        {
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false,
            OmitXmlDeclaration = true,
            Async = true,
        };

        await using (XmlWriter xmlWriter = XmlWriter.Create(outputFileName: project, settings: settings))
        {
            doc.Save(xmlWriter);
            await ValueTask.CompletedTask;
        }
    }

    private static async ValueTask<(XmlDocument doc, string content)> LoadProjectAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        string content = await LoadProjectTextAsync(path: path, cancellationToken: cancellationToken);
        XmlDocument doc = new();

        doc.LoadXml(content);

        return (doc, content);
    }

    private static Task<string> LoadProjectTextAsync(string path, in CancellationToken cancellationToken)
    {
        return File.ReadAllTextAsync(path: path, encoding: Encoding.UTF8, cancellationToken: cancellationToken);
    }

    private async ValueTask<CleanupUpdateContext> BuildUpdateContextAsync(
        IGitRepository templateRepo,
        string workFolder,
        string trackingFileName,
        string releaseConfigFileName,
        CancellationToken cancellationToken
    )
    {
        DotNetVersionSettings dotNetSettings = await this._globalJson.LoadGlobalJsonAsync(
            baseFolder: templateRepo.WorkingDirectory,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<Version> installedDotNetSdks = await this._dotNetVersion.GetInstalledSdksAsync(cancellationToken);

        if (
            dotNetSettings.SdkVersion is not null
            && Version.TryParse(input: dotNetSettings.SdkVersion, out Version? sdkVersion)
        )
        {
            if (!installedDotNetSdks.Contains(sdkVersion))
            {
                this._logger.LogMissingSdk(sdkVersion: sdkVersion, installedSdks: installedDotNetSdks);

                throw new DotNetBuildErrorException("SDK version specified in global.json is not installed");
            }
        }

        ReleaseConfig releaseConfig = await this._releaseConfigLoader.LoadAsync(
            path: releaseConfigFileName,
            cancellationToken: cancellationToken
        );

        return new(
            WorkFolder: workFolder,
            TrackingFileName: trackingFileName,
            DotNetSettings: dotNetSettings,
            ReleaseConfig: releaseConfig
        );
    }

    private ValueTask LoadTrackingCacheAsync(string? trackingFile, in CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingFile) || !File.Exists(trackingFile))
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
