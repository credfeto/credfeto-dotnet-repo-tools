using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.ChangeLog;
using Credfeto.ChangeLog.Exceptions;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.Packages.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Exceptions;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Interfaces;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;
using Credfeto.DotNet.Repo.Tracking.Interfaces;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class BulkTemplateUpdater : IBulkTemplateUpdater
{
    private const string CHANGELOG_ENTRY_TYPE = "Changed";

    private const string DOT_GITHUB_DIR = ".github";
    private readonly IBulkPackageConfigLoader _bulkPackageConfigLoader;
    private readonly IDependaBotConfigBuilder _dependaBotConfigBuilder;
    private readonly IDotNetBuild _dotNetBuild;

    private readonly IDotNetFilesDetector _dotNetFilesDetector;
    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IFileUpdater _fileUpdater;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly ILabelsBuilder _labelsBuilder;
    private readonly ILogger<BulkTemplateUpdater> _logger;

    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly IReleaseGeneration _releaseGeneration;
    private readonly ITemplateConfigLoader _templateConfigLoader;
    private readonly ITrackingCache _trackingCache;

    public BulkTemplateUpdater(ITrackingCache trackingCache,
                               IGlobalJson globalJson,
                               IDotNetFilesDetector dotNetFilesDetector,
                               IDotNetVersion dotNetVersion,
                               IDotNetSolutionCheck dotNetSolutionCheck,
                               IDotNetBuild dotNetBuild,
                               IReleaseConfigLoader releaseConfigLoader,
                               IReleaseGeneration releaseGeneration,
                               IGitRepositoryFactory gitRepositoryFactory,
                               IBulkPackageConfigLoader bulkPackageConfigLoader,
                               IFileUpdater fileUpdater,
                               IDependaBotConfigBuilder dependaBotConfigBuilder,
                               ILabelsBuilder labelsBuilder,
                               ITemplateConfigLoader templateConfigLoader,
                               ILogger<BulkTemplateUpdater> logger)
    {
        this._trackingCache = trackingCache;
        this._globalJson = globalJson;
        this._dotNetFilesDetector = dotNetFilesDetector;
        this._dotNetVersion = dotNetVersion;
        this._dotNetSolutionCheck = dotNetSolutionCheck;
        this._dotNetBuild = dotNetBuild;
        this._releaseConfigLoader = releaseConfigLoader;
        this._releaseGeneration = releaseGeneration;
        this._gitRepositoryFactory = gitRepositoryFactory;
        this._bulkPackageConfigLoader = bulkPackageConfigLoader;
        this._fileUpdater = fileUpdater;
        this._dependaBotConfigBuilder = dependaBotConfigBuilder;
        this._labelsBuilder = labelsBuilder;
        this._templateConfigLoader = templateConfigLoader;
        this._logger = logger;
    }

    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
                                           string templateConfigFileName,
                                           string releaseConfigFileName,
                                           IReadOnlyList<string> repositories,
                                           CancellationToken cancellationToken)
    {
        await this.LoadTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);

        IReadOnlyList<PackageUpdate> packages = await this._bulkPackageConfigLoader.LoadAsync(path: packagesFileName, cancellationToken: cancellationToken);

        using (IGitRepository templateRepo = await this._gitRepositoryFactory.OpenOrCloneAsync(workDir: workFolder, repoUrl: templateRepository, cancellationToken: cancellationToken))
        {
            TemplateUpdateContext updateContext = await this.BuildUpdateContextAsync(templateRepo: templateRepo,
                                                                                     workFolder: workFolder,
                                                                                     trackingFileName: trackingFileName,
                                                                                     templateConfigFileName: templateConfigFileName,
                                                                                     releaseConfigFileName: releaseConfigFileName,
                                                                                     cancellationToken: cancellationToken);

            try
            {
                await this.UpdateRepositoriesAsync(updateContext: updateContext, repositories: repositories, packages: packages, cancellationToken: cancellationToken);
            }
            finally
            {
                await this.SaveTrackingCacheAsync(trackingFile: trackingFileName, cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask UpdateRepositoriesAsync(TemplateUpdateContext updateContext, IReadOnlyList<string> repositories, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
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
                catch (GitRepositoryLockedException exception)
                {
                    this._logger.LogRepoLocked(repo, exception.Message,exception: exception);
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

    private async ValueTask UpdateRepositoryAsync(TemplateUpdateContext updateContext, IReadOnlyList<PackageUpdate> packages, string repo, CancellationToken cancellationToken)
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

    private async ValueTask ProcessRepoUpdatesAsync(TemplateUpdateContext updateContext, RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        DotNetFiles dotNetFiles = await this._dotNetFilesDetector.FindAsync(baseFolder: repoContext.WorkingDirectory, cancellationToken: cancellationToken);

        int totalUpdates = await this.UpdateStandardFilesAsync(updateContext: updateContext,
                                                               repoContext: repoContext,
                                                               dotNetFiles: dotNetFiles,
                                                               packages: packages,
                                                               cancellationToken: cancellationToken);

        if (dotNetFiles.HasSolutions)
        {
            await this.UpdateDotNetAsync(updateContext: updateContext,
                                         repoContext: repoContext,
                                         packages: packages,
                                         dotNetFiles: dotNetFiles,
                                         totalUpdates: totalUpdates,
                                         cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogNoDotNetFilesFound();
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: repoContext.Repository.HeadRev, cancellationToken: cancellationToken);
        }

        await RemoveOldFilesAsync(updateContext: updateContext, repoContext: repoContext, cancellationToken: cancellationToken);
    }

    private static async ValueTask RemoveOldFilesAsync(TemplateUpdateContext updateContext, RepoContext repoContext, CancellationToken cancellationToken)
    {
        foreach ((string fileName, string prefix) in updateContext.TemplateConfig.Cleanup.Files)
        {
            string repoFile = Path.Combine(path1: repoContext.WorkingDirectory, path2: fileName);

            if (File.Exists(repoFile))
            {
                File.Delete(repoFile);
                await repoContext.Repository.CommitAsync($"Removed: {prefix}", cancellationToken: cancellationToken);
                await repoContext.Repository.PushAsync(cancellationToken: cancellationToken);
                await repoContext.Repository.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
            }
        }
    }

    private async ValueTask<int> UpdateStandardFilesAsync(TemplateUpdateContext updateContext,
                                                          RepoContext repoContext,
                                                          DotNetFiles dotNetFiles,
                                                          IReadOnlyList<PackageUpdate> packages,
                                                          CancellationToken cancellationToken)
    {
        FileContext fileContext = new(UpdateContext: updateContext, RepoContext: repoContext);

        int changes = 0;
        IEnumerable<CopyInstruction> filesToUpdate = GetStandardFilesToUpdate(fileContext);

        changes += await this.MakeCopyInstructionChangesAsync(repoContext: repoContext, filesToUpdate: filesToUpdate, cancellationToken: cancellationToken);

        if (await this.UpdateDependabotConfigAsync(updateContext: updateContext, repoContext: repoContext, dotNetFiles: dotNetFiles, packages: packages, cancellationToken: cancellationToken))
        {
            ++changes;
        }

        return changes;
    }

    private async ValueTask<int> MakeCopyInstructionChangesAsync(RepoContext repoContext, IEnumerable<CopyInstruction> filesToUpdate, CancellationToken cancellationToken)
    {
        int changes = 0;

        foreach (CopyInstruction copyInstruction in filesToUpdate)
        {
            bool changed = await this._fileUpdater.UpdateFileAsync(repoContext: repoContext,
                                                                   copyInstruction: copyInstruction,
                                                                   changelogUpdate: NoChangeLogUpdateAsync,
                                                                   cancellationToken: cancellationToken);

            if (changed)
            {
                ++changes;
                await repoContext.Repository.PushAsync(cancellationToken);
            }
        }

        return changes;
    }

    private async ValueTask<bool> UpdateDependabotConfigAsync(TemplateUpdateContext updateContext,
                                                              RepoContext repoContext,
                                                              DotNetFiles dotNetFiles,
                                                              IReadOnlyList<PackageUpdate> packages,
                                                              CancellationToken cancellationToken)
    {
        if (!updateContext.TemplateConfig.GitHub.Dependabot.Generate)
        {
            return false;
        }

        string dependabotConfig = Path.Combine(path1: repoContext.WorkingDirectory, path2: DOT_GITHUB_DIR, path3: "dependabot.yml");

        string newConfig = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(repoContext: repoContext,
                                                                                          templateFolder: updateContext.TemplateFolder,
                                                                                          dotNetFiles: dotNetFiles,
                                                                                          packages: packages,
                                                                                          cancellationToken: cancellationToken);
        byte[] newConfigBytes = Encoding.UTF8.GetBytes(newConfig);

        bool writeNewConfig = false;

        if (File.Exists(dependabotConfig))
        {
            byte[] existingConfigBytes = await File.ReadAllBytesAsync(path: dependabotConfig, cancellationToken: cancellationToken);

            if (!existingConfigBytes.SequenceEqual(newConfigBytes))
            {
                // content changed.
                writeNewConfig = true;
            }
        }

        if (writeNewConfig)
        {
            await File.WriteAllBytesAsync(path: dependabotConfig, bytes: newConfigBytes, cancellationToken: cancellationToken);
            await repoContext.Repository.CommitAsync(message: "[Dependabot] Updated configuration", cancellationToken: cancellationToken);
            await repoContext.Repository.PushAsync(cancellationToken);

            return true;
        }

        return false;
    }

    private static ValueTask NoChangeLogUpdateAsync(CancellationToken cancellationToken)
    {
        // nothing to do here
        return ValueTask.CompletedTask;
    }

    private static IEnumerable<CopyInstruction> GetStandardFilesToUpdate(in FileContext fileContext)
    {
        string issueTemplates = Path.Combine(path1: fileContext.UpdateContext.TemplateFolder, path2: DOT_GITHUB_DIR, path3: "ISSUE_TEMPLATE");
        string actions = Path.Combine(path1: fileContext.UpdateContext.TemplateFolder, path2: DOT_GITHUB_DIR, path3: "actions");
        string workflows = Path.Combine(path1: fileContext.UpdateContext.TemplateFolder, path2: DOT_GITHUB_DIR, path3: "workflows");
        string linters = Path.Combine(path1: fileContext.UpdateContext.TemplateFolder, path2: DOT_GITHUB_DIR, path3: "linters");

        Func<byte[], (byte[] bytes, bool changed)> rewriteRunsOn = ShouldUseGitHubHostedRunners(fileContext);

        TemplateConfig templateConfig = fileContext.UpdateContext.TemplateConfig;

        List<CopyInstruction> copyInstructions = [];

        foreach ((string fileName, string context) in templateConfig.General.Files)
        {
            copyInstructions.Add(fileContext.MakeFile(fileName: fileName, prefix: context));
        }

        if (templateConfig.GitHub.IssueTemplates)
        {
            copyInstructions.AddRange(IncludeFilesInSource(fileContext: fileContext, sourceFolder: issueTemplates, prefix: "Config", search: "*"));
        }

        if (templateConfig.GitHub.PullRequestTemplates)
        {
            copyInstructions.Add(fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, DOT_GITHUB_DIR, "PULL_REQUEST_TEMPLATE.md"), prefix: "Config"));
        }

        if (templateConfig.GitHub.Actions)
        {
            copyInstructions.AddRange(IncludeFilesInSource(fileContext: fileContext, sourceFolder: actions, prefix: "Actions", search: "action.yml"));
            copyInstructions.AddRange(IncludeFilesInSource(fileContext: fileContext, sourceFolder: workflows, search: "*.yml", prefix: "Actions", apply: rewriteRunsOn));
        }

        if (templateConfig.GitHub.Linters)
        {
            copyInstructions.AddRange(IncludeFilesInSource(fileContext: fileContext, sourceFolder: linters, prefix: "Linters", search: "*"));
        }

        foreach ((string fileName, string context) in templateConfig.GitHub.Files)
        {
            copyInstructions.Add(fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, DOT_GITHUB_DIR, fileName), prefix: context));
        }

        return copyInstructions;
    }

    private static Func<byte[], (byte[] bytes, bool changed)> ShouldUseGitHubHostedRunners(in FileContext fileContext)
    {
        return fileContext.RepoContext.ClonePath.Contains(value: "credfeto", comparisonType: StringComparison.Ordinal)
            ? ConvertFromSelfHostedRunnerToGitHubHostedRunner
            : NoChange;
    }

    private static (byte[] bytes, bool changed) ConvertFromSelfHostedRunnerToGitHubHostedRunner(byte[] source)
    {
        StringBuilder stringBuilder = new(Encoding.UTF8.GetString(source));

        const string ubuntuLatest = "runs-on: ubuntu-latest";

        stringBuilder = stringBuilder.Replace(oldValue: "runs-on: [self-hosted, linux]", newValue: ubuntuLatest)
                                     .Replace(oldValue: "runs-on: [self-hosted, linux, build]", newValue: ubuntuLatest)
                                     .Replace(oldValue: "runs-on: [self-hosted, linux, deploy]", newValue: ubuntuLatest);

        byte[] target = Encoding.UTF8.GetBytes(stringBuilder.ToString());

        if (source.SequenceEqual(target))
        {
            return (source, false);
        }

        return (target, true);
    }

    private static IEnumerable<CopyInstruction> IncludeFilesInSource(in FileContext fileContext, string sourceFolder, string prefix, string search)
    {
        return IncludeFilesInSource(fileContext: fileContext, sourceFolder: sourceFolder, search: search, prefix: prefix, apply: NoChange);
    }

    private static IEnumerable<CopyInstruction> IncludeFilesInSource(FileContext fileContext, string sourceFolder, string search, string prefix, Func<byte[], (byte[] bytes, bool changed)> apply)
    {
        string sourceFolderName = Path.Combine(path1: fileContext.UpdateContext.TemplateFolder, path2: sourceFolder);

        int sourceFolderNamePrefixLength = TemplateFolderLength(fileContext);

        if (Directory.Exists(sourceFolderName))
        {
            return Directory.EnumerateFiles(path: sourceFolderName, searchPattern: search, searchOption: SearchOption.AllDirectories)
                            .Select(issueSourceFile => issueSourceFile[sourceFolderNamePrefixLength..])
                            .Select(fileName => fileContext.MakeFile(fileName: fileName, prefix: prefix, apply: apply));
        }

        return [];
    }

    private static int TemplateFolderLength(in FileContext fileContext)
    {
        return fileContext.UpdateContext.TemplateFolder.Length + 1;
    }

    private async ValueTask UpdateDotNetAsync(TemplateUpdateContext updateContext,
                                              RepoContext repoContext,
                                              IReadOnlyList<PackageUpdate> packages,
                                              DotNetFiles dotNetFiles,
                                              int totalUpdates,
                                              CancellationToken cancellationToken)
    {
        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(projects: dotNetFiles.Projects, cancellationToken: cancellationToken);

        // Always update global.json if needed before checking updating the last known good build
        string repoGlobalJson = Path.Combine(path1: dotNetFiles.SourceDirectory, path2: "global.json");

        if (File.Exists(repoGlobalJson))
        {
            bool changed = await this.UpdateGlobalJsonAsync(repoContext: repoContext,
                                                       updateContext: updateContext,
                                                       dotNetFiles: dotNetFiles,
                                                       buildSettings: buildSettings,
                                                       cancellationToken: cancellationToken);

            if (changed)
            {
                ++totalUpdates;
            }
        }

        totalUpdates += await this.UpdateResharperSettingsAsync(repoContext: repoContext, updateContext: updateContext, dotNetFiles: dotNetFiles, cancellationToken: cancellationToken);

        FileContext fileContext = new(UpdateContext: updateContext, RepoContext: repoContext);
        IEnumerable<CopyInstruction> filesToUpdate = GetDotNetFilesToUpdate(fileContext);

        totalUpdates += await this.MakeCopyInstructionChangesAsync(repoContext: repoContext, filesToUpdate: filesToUpdate, cancellationToken: cancellationToken);

        if (await this.UpdateLabelAsync(updateContext: updateContext, repoContext: repoContext, projects: dotNetFiles.Projects, cancellationToken: cancellationToken))
        {
            ++totalUpdates;
        }

        if (totalUpdates == 0)
        {
            // no updates in this run - so might be able to create a release
            await this._releaseGeneration.TryCreateNextPatchAsync(repoContext: repoContext,
                                                                  dotNetFiles: dotNetFiles,
                                                                  buildSettings: buildSettings,
                                                                  dotNetSettings: updateContext.DotNetSettings,
                                                                  packages: packages,
                                                                  releaseConfig: updateContext.ReleaseConfig,
                                                                  cancellationToken: cancellationToken);
        }
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Needs Review")]
    private async ValueTask<bool> UpdateLabelAsync(TemplateUpdateContext updateContext, RepoContext repoContext, IReadOnlyList<string> projects, CancellationToken cancellationToken)
    {
        if (!updateContext.TemplateConfig.GitHub.Labels.Generate)
        {
            return false;
        }

        string labelsFileName = Path.Combine(path1: repoContext.WorkingDirectory, path2: DOT_GITHUB_DIR, path3: "labels.yml");
        string labelersFileName = Path.Combine(path1: repoContext.WorkingDirectory, path2: DOT_GITHUB_DIR, path3: "labeler.yml");

        (string labels, string labeler) = this._labelsBuilder.BuildLabelsConfig(projects: projects);

        bool changed = false;

        if (File.Exists(labelsFileName))
        {
            string content = await File.ReadAllTextAsync(path: labelsFileName, cancellationToken: cancellationToken);

            if (!StringComparer.Ordinal.Equals(x: content, y: labels))
            {
                changed = true;
            }
        }

        if (!changed && File.Exists(labelersFileName))
        {
            string content = await File.ReadAllTextAsync(path: labelersFileName, cancellationToken: cancellationToken);

            if (!StringComparer.Ordinal.Equals(x: content, y: labeler))
            {
                changed = true;
            }
        }

        if (changed)
        {
            await File.WriteAllTextAsync(path: labelsFileName, contents: labels, cancellationToken: cancellationToken);
            await File.WriteAllTextAsync(path: labelersFileName, contents: labeler, cancellationToken: cancellationToken);
            await repoContext.Repository.CommitAsync(message: "[PR] Updated labels", cancellationToken: cancellationToken);
            await repoContext.Repository.PushAsync(cancellationToken);

            return true;
        }

        return false;
    }

    private static IEnumerable<CopyInstruction> GetDotNetFilesToUpdate(FileContext fileContext)
    {
        foreach ((string fileName, string context) in fileContext.UpdateContext.TemplateConfig.DotNet.Files)
        {
            yield return fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, "src", fileName), prefix: context);
        }
    }

    private async ValueTask<int> UpdateResharperSettingsAsync(RepoContext repoContext, TemplateUpdateContext updateContext, DotNetFiles dotNetFiles, CancellationToken cancellationToken)
    {
        if (!updateContext.TemplateConfig.DotNet.JetBrainsDotSettings)
        {
            return 0;
        }

        const string dotSettingsExtension = ".DotSettings";

        string templateSourceDirectory = Path.Combine(path1: updateContext.TemplateFolder, path2: "src");
        string? dotSettingsSourceFile = Directory.GetFiles(path: templateSourceDirectory, "*.sln" + dotSettingsExtension)
                                                 .Concat(Directory.GetFiles(path: templateSourceDirectory, "*.slnx" + dotSettingsExtension))
                                                 .FirstOrDefault();

        if (string.IsNullOrEmpty(dotSettingsSourceFile))
        {
            return 0;
        }

        int changes = 0;

        foreach (string targetSolutionFileName in dotNetFiles.Solutions)
        {
            string repoRelativeSolutionFileName = targetSolutionFileName[(repoContext.WorkingDirectory.Length + 1)..];
            string targetFileName = targetSolutionFileName + dotSettingsExtension;

            string commitMessage = $"Updated Resharper settings for {repoRelativeSolutionFileName}";
            CopyInstruction copyInstruction = new(SourceFileName: dotSettingsSourceFile, TargetFileName: targetFileName, Apply: NoChange, IsTargetNewer: NoVersionCheck, Message: commitMessage);

            bool changed = await this._fileUpdater.UpdateFileAsync(repoContext: repoContext,
                                                                   copyInstruction: copyInstruction,
                                                                   changelogUpdate: NoChangeLogUpdateAsync,
                                                                   cancellationToken: cancellationToken);

            if (changed)
            {
                ++changes;
                await repoContext.Repository.PushAsync(cancellationToken);
            }
        }

        return changes;
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "To be refactored")]
    private async ValueTask<bool> UpdateGlobalJsonAsync(RepoContext repoContext,
                                                        TemplateUpdateContext updateContext,
                                                        DotNetFiles dotNetFiles,
                                                        BuildSettings buildSettings,
                                                        CancellationToken cancellationToken)
    {
        if (!updateContext.TemplateConfig.DotNet.GlobalJson)
        {
            return false;
        }

        string templateGlobalJsonFileName = Path.Combine(path1: updateContext.TemplateFolder, path2: "src", path3: "global.json");
        string targetGlobalJsonFileName = Path.Combine(path1: dotNetFiles.SourceDirectory, path2: "global.json");

        const string messagePrefix = "SDK - Updated DotNet SDK to ";
        string message = messagePrefix + updateContext.DotNetSettings.SdkVersion;

        const string branchPrefix = "depends/dotnet/sdk/";
        string branchName = branchPrefix + updateContext.DotNetSettings.SdkVersion;
        string invalidBranchName = branchPrefix + Guid.NewGuid();

        DotNetVersionSettings solutionDotNetSettings = await this._globalJson.LoadGlobalJsonAsync(baseFolder: repoContext.WorkingDirectory, cancellationToken: cancellationToken);

        bool branchCreated = false;

        try
        {
            CopyInstruction copyInstruction = new(SourceFileName: templateGlobalJsonFileName,
                                                  TargetFileName: targetGlobalJsonFileName,
                                                  Apply: NoChange,
                                                  IsTargetNewer: GlobalSdkVersionCheck,
                                                  Message: message);
            bool changed = await this._fileUpdater.UpdateFileAsync(repoContext: repoContext,
                                                                   copyInstruction: copyInstruction,
                                                                   changelogUpdate: ChangelogUpdateAsync,
                                                                   cancellationToken: cancellationToken);

            if (changed)
            {
                if (branchCreated)
                {
                    await repoContext.Repository.PushOriginAsync(branchName: branchName, upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
                    await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: branchName,
                                                                              branchPrefix: branchPrefix,
                                                                              upstream: GitConstants.Upstream,
                                                                              cancellationToken: cancellationToken);
                    await repoContext.Repository.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
                }
                else
                {
                    await repoContext.Repository.PushAsync(cancellationToken);
                    await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: invalidBranchName,
                                                                              branchPrefix: branchPrefix,
                                                                              upstream: GitConstants.Upstream,
                                                                              cancellationToken: cancellationToken);
                }

                string lastKnownGoodBuild = repoContext.Repository.HeadRev;
                await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: lastKnownGoodBuild, cancellationToken: cancellationToken);
            }
            else
            {
                await repoContext.Repository.RemoveBranchesForPrefixAsync(branchForUpdate: invalidBranchName,
                                                                          branchPrefix: branchPrefix,
                                                                          upstream: GitConstants.Upstream,
                                                                          cancellationToken: cancellationToken);
            }

            return changed;
        }
        catch (BranchAlreadyExistsException)
        {
            return false;
        }
        finally
        {
            await repoContext.Repository.ResetToDefaultBranchAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        }

        async ValueTask ChangelogUpdateAsync(CancellationToken token)
        {
            await ChangeLogUpdater.RemoveEntryAsync(changeLogFileName: repoContext.ChangeLogFileName, type: CHANGELOG_ENTRY_TYPE, message: messagePrefix, cancellationToken: token);
            await ChangeLogUpdater.AddEntryAsync(changeLogFileName: repoContext.ChangeLogFileName, type: CHANGELOG_ENTRY_TYPE, message: message, cancellationToken: token);

            bool ok = dotNetFiles.Projects is [] || await this.CheckBuildAsync(updateContext: updateContext, dotNetFiles: dotNetFiles, buildSettings: buildSettings, cancellationToken: token);

            if (!ok)
            {
                if (repoContext.Repository.DoesBranchExist(branchName))
                {
                    throw new BranchAlreadyExistsException(branchName);
                }

                await repoContext.Repository.CreateBranchAsync(branchName: branchName, cancellationToken: token);

                branchCreated = true;
            }
        }

        bool GlobalSdkVersionCheck(byte[] source, byte[] target)
        {
            if (solutionDotNetSettings.SdkVersion is null)
            {
                // older/invalid
                this._logger.LogInformation("No Target SDK Version");

                return false;
            }

            if (updateContext.DotNetSettings.SdkVersion is null)
            {
                // Newer
                this._logger.LogInformation("No Source SDK Version");

                return true;
            }

            NuGetVersion targetVersion = new(solutionDotNetSettings.SdkVersion);
            NuGetVersion sourceVersion = new(updateContext.DotNetSettings.SdkVersion);

            this._logger.LogInformation(message: "Comparing SDK Versions: {Source} -> {Target}", sourceVersion, targetVersion);

            return VersionCheck.IsDotNetSdkTargetNewer(sourceVersion: sourceVersion, targetVersion: targetVersion);
        }
    }

    private static (byte[] source, bool changed) NoChange(byte[] source)
    {
        return (source, false);
    }

    private static bool NoVersionCheck(byte[] source, byte[] target)
    {
        return false;
    }

    private async ValueTask<bool> CheckBuildAsync(TemplateUpdateContext updateContext, DotNetFiles dotNetFiles, BuildSettings buildSettings, CancellationToken cancellationToken)
    {
        try
        {
            BuildContext buildContext = new(SourceDirectory: dotNetFiles.SourceDirectory, BuildSettings: buildSettings, new(PreRelease: true));
            await this._dotNetSolutionCheck.PreCheckAsync(solutions: dotNetFiles.Solutions,
                                                          repositoryDotNetSettings: updateContext.DotNetSettings,
                                                          templateDotNetSettings: updateContext.DotNetSettings,
                                                          cancellationToken: cancellationToken);

            await this._dotNetBuild.BuildAsync(buildContext: buildContext, cancellationToken: cancellationToken);

            return true;
        }
        catch (SolutionCheckFailedException)
        {
            return false;
        }
        catch (DotNetBuildErrorException)
        {
            return false;
        }
    }

    private async ValueTask<TemplateUpdateContext> BuildUpdateContextAsync(IGitRepository templateRepo,
                                                                           string workFolder,
                                                                           string trackingFileName,
                                                                           string templateConfigFileName,
                                                                           string releaseConfigFileName,
                                                                           CancellationToken cancellationToken)
    {
        TemplateConfig templateConfig = await this._templateConfigLoader.LoadConfigAsync(path: templateConfigFileName, cancellationToken: cancellationToken);

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
                   TemplateFolder: templateRepo.WorkingDirectory,
                   TrackingFileName: trackingFileName,
                   TemplateConfig: templateConfig,
                   DotNetSettings: dotNetSettings,
                   ReleaseConfig: releaseConfig);
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

    [DebuggerDisplay("Template: {UpdateContext.TemplateFolder}, Repo: {RepoContext.WorkingDirectory}")]
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct FileContext(TemplateUpdateContext UpdateContext, RepoContext RepoContext)
    {
        public CopyInstruction MakeFile(string fileName, string prefix)
        {
            return this.MakeFile(fileName: fileName, prefix: prefix, apply: NoChange);
        }

        public CopyInstruction MakeFile(string fileName, string prefix, Func<byte[], (byte[] bytes, bool changed)> apply)
        {
            string sourceFileName = Path.Combine(path1: this.UpdateContext.TemplateFolder, path2: fileName);
            string targetFileName = Path.Combine(path1: this.RepoContext.WorkingDirectory, path2: fileName);

            return new(SourceFileName: sourceFileName, TargetFileName: targetFileName, Apply: apply, IsTargetNewer: (_, _) => false, $"[{prefix}] Updated {fileName}");
        }
    }
}