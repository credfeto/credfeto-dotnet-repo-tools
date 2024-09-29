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
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
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
    private readonly IDotNetSolutionCheck _dotNetSolutionCheck;
    private readonly IDotNetVersion _dotNetVersion;
    private readonly IFileUpdater _fileUpdater;
    private readonly IGitRepositoryFactory _gitRepositoryFactory;
    private readonly IGlobalJson _globalJson;
    private readonly ILabelsBuilder _labelsBuilder;
    private readonly ILogger<BulkTemplateUpdater> _logger;
    private readonly IReleaseConfigLoader _releaseConfigLoader;
    private readonly IReleaseGeneration _releaseGeneration;
    private readonly ITrackingCache _trackingCache;

    public BulkTemplateUpdater(ITrackingCache trackingCache,
                               IGlobalJson globalJson,
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
                               ILogger<BulkTemplateUpdater> logger)
    {
        this._trackingCache = trackingCache;
        this._globalJson = globalJson;
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
        this._logger = logger;
    }

    public async ValueTask BulkUpdateAsync(string templateRepository,
                                           string trackingFileName,
                                           string packagesFileName,
                                           string workFolder,
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
        string? lastKnownGoodBuild = this._trackingCache.Get(repoContext.ClonePath);

        int totalUpdates = await this.UpdateStandardFilesAsync(updateContext: updateContext, repoContext: repoContext, packages: packages, cancellationToken: cancellationToken);

        if (repoContext.HasDotNetSolutions(out string? sourceDirectory, out IReadOnlyList<string>? solutions))
        {
            await this.UpdateDotNetAsync(updateContext: updateContext,
                                         repoContext: repoContext,
                                         packages: packages,
                                         lastKnownGoodBuild: lastKnownGoodBuild,
                                         solutions: solutions,
                                         sourceDirectory: sourceDirectory,
                                         totalUpdates: totalUpdates,
                                         cancellationToken: cancellationToken);
        }
        else
        {
            this._logger.LogNoDotNetFilesFound();
            await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: repoContext.Repository.HeadRev, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<int> UpdateStandardFilesAsync(TemplateUpdateContext updateContext, RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        FileContext fileContext = new(UpdateContext: updateContext, RepoContext: repoContext);

        int changes = 0;
        IEnumerable<CopyInstruction> filesToUpdate = GetStandardFilesToUpdate(fileContext);

        changes += await this.MakeCopyInstructionChangesAsync(repoContext: repoContext, filesToUpdate: filesToUpdate, cancellationToken: cancellationToken);

        // TODO: Implement
/*
-- Remove Obsolete Workflows(from config)
-- Remove Obsolete Actions(from config)
 */
        if (await this.UpdateDependabotConfigAsync(updateContext: updateContext, repoContext: repoContext, packages: packages, cancellationToken: cancellationToken))
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

    private async ValueTask<bool> UpdateDependabotConfigAsync(TemplateUpdateContext updateContext, RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        string dependabotConfig = Path.Combine(path1: repoContext.WorkingDirectory, path2: DOT_GITHUB_DIR, path3: "dependabot.yml");

        string newConfig = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(repoContext: repoContext,
                                                                                          templateFolder: updateContext.TemplateFolder,
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

        return GetStandardFilesBaseToUpdate(fileContext)
               .Concat(IncludeFilesInSource(fileContext: fileContext, sourceFolder: issueTemplates, prefix: "Config"))
               .Concat(IncludeFilesInSource(fileContext: fileContext, sourceFolder: actions, prefix: "Actions"))
               .Concat(IncludeFilesInSource(fileContext: fileContext, sourceFolder: workflows, prefix: "Actions", apply: rewriteRunsOn))
               .Concat(IncludeFilesInSource(fileContext: fileContext, sourceFolder: linters, prefix: "Linters"));
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

        stringBuilder = stringBuilder.Replace(oldValue: "runs-on: [self-hosted, linux]", newValue: "runs-on: ubuntu-latest");

        byte[] target = Encoding.UTF8.GetBytes(stringBuilder.ToString());

        if (source.SequenceEqual(target))
        {
            return (source, false);
        }

        return (target, true);
    }

    private static IEnumerable<CopyInstruction> GetStandardFilesBaseToUpdate(in FileContext fileContext)
    {
        return
        [
            fileContext.MakeFile(fileName: ".editorconfig", prefix: "Config"),
            fileContext.MakeFile(fileName: ".gitleaks.toml", prefix: "Config"),
            fileContext.MakeFile(fileName: ".gitignore", prefix: "Config"),
            fileContext.MakeFile(fileName: ".gitattributes", prefix: "Config"),
            fileContext.MakeFile(fileName: ".tsqllintrc", prefix: "Linters"),
            fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, DOT_GITHUB_DIR, "pr-lint.yml"), prefix: "Linters"),
            fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, DOT_GITHUB_DIR, "CODEOWNERS"), prefix: "Config"),
            fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, DOT_GITHUB_DIR, "PULL_REQUEST_TEMPLATE.md"), prefix: "Config"),
            fileContext.MakeFile(fileName: "CONTRIBUTING.md", prefix: "Documentation"),
            fileContext.MakeFile(fileName: "SECURITY.md", prefix: "Documentation")
        ];
    }

    private static IEnumerable<CopyInstruction> IncludeFilesInSource(in FileContext fileContext, string sourceFolder, string prefix)
    {
        return IncludeFilesInSource(fileContext: fileContext, sourceFolder: sourceFolder, prefix: prefix, apply: NoChange);
    }

    private static IEnumerable<CopyInstruction> IncludeFilesInSource(FileContext fileContext, string sourceFolder, string prefix, Func<byte[], (byte[] bytes, bool changed)> apply)
    {
        string sourceFolderName = Path.Combine(path1: fileContext.UpdateContext.TemplateFolder, path2: sourceFolder);

        int sourceFolderNamePrefixLength = TemplateFolderLength(fileContext);

        if (Directory.Exists(sourceFolderName))
        {
            return Directory.EnumerateFiles(path: sourceFolderName, searchPattern: "*", searchOption: SearchOption.AllDirectories)
                            .Select(issueSourceFile => issueSourceFile[sourceFolderNamePrefixLength..])
                            .Select(fileName => fileContext.MakeFile(fileName: fileName, prefix: prefix, apply: apply));
        }

        return [];
    }

    private static int TemplateFolderLength(in FileContext fileContext)
    {
        return fileContext.UpdateContext.TemplateFolder.Length + 1;
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Debug logging")]
    private async ValueTask UpdateDotNetAsync(TemplateUpdateContext updateContext,
                                              RepoContext repoContext,
                                              IReadOnlyList<PackageUpdate> packages,
                                              string? lastKnownGoodBuild,
                                              IReadOnlyList<string> solutions,
                                              string sourceDirectory,
                                              int totalUpdates,
                                              CancellationToken cancellationToken)
    {
        IReadOnlyList<string> projects = Directory.GetFiles(path: sourceDirectory, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);

        BuildSettings buildSettings = await this._dotNetBuild.LoadBuildSettingsAsync(projects: projects, cancellationToken: cancellationToken);

        if (lastKnownGoodBuild is null || !StringComparer.OrdinalIgnoreCase.Equals(x: lastKnownGoodBuild, y: repoContext.Repository.HeadRev))
        {
            string repoGlobalJson = Path.Combine(path1: sourceDirectory, path2: "global.json");

            if (File.Exists(repoGlobalJson))
            {
                DotNetVersionSettings repoDotNetSettings = await this._globalJson.LoadGlobalJsonAsync(baseFolder: repoContext.WorkingDirectory, cancellationToken: cancellationToken);

                if (projects is not [])
                {
                    await this._dotNetSolutionCheck.PreCheckAsync(solutions: solutions, dotNetSettings: repoDotNetSettings, buildSettings: buildSettings, cancellationToken: cancellationToken);

                    if (StringComparer.Ordinal.Equals(x: updateContext.DotNetSettings.SdkVersion, y: repoDotNetSettings.SdkVersion))
                    {
                        await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);

                        lastKnownGoodBuild = repoContext.Repository.HeadRev;
                        await this._trackingCache.UpdateTrackingAsync(repoContext: repoContext, updateContext: updateContext, value: lastKnownGoodBuild, cancellationToken: cancellationToken);
                    }
                }
            }
        }

        bool changed = await this.UpdateGlobalJsonAsync(repoContext: repoContext,
                                                        updateContext: updateContext,
                                                        sourceDirectory: sourceDirectory,
                                                        solutions: solutions,
                                                        projects: projects,
                                                        buildSettings: buildSettings,
                                                        cancellationToken: cancellationToken);

        if (changed)
        {
            ++totalUpdates;
        }

        totalUpdates += await this.UpdateResharperSettingsAsync(repoContext: repoContext, updateContext: updateContext, solutions: solutions, cancellationToken: cancellationToken);

        FileContext fileContext = new(UpdateContext: updateContext, RepoContext: repoContext);
        IEnumerable<CopyInstruction> filesToUpdate = GetDotNetFilesToUpdate(fileContext);

        totalUpdates += await this.MakeCopyInstructionChangesAsync(repoContext: repoContext, filesToUpdate: filesToUpdate, cancellationToken: cancellationToken);

        if (await this.UpdateLabelAsync(repoContext: repoContext, projects: projects, cancellationToken: cancellationToken))
        {
            ++totalUpdates;
        }

        // TODO
/*
   updateLabel -baseFolder $targetRepo
 */

        if (totalUpdates == 0)
        {
            // no updates in this run - so might be able to create a release
            await this._releaseGeneration.TryCreateNextPatchAsync(repoContext: repoContext,
                                                                  basePath: sourceDirectory,
                                                                  buildSettings: buildSettings,
                                                                  dotNetSettings: updateContext.DotNetSettings,
                                                                  solutions: solutions,
                                                                  packages: packages,
                                                                  releaseConfig: updateContext.ReleaseConfig,
                                                                  cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<bool> UpdateLabelAsync(RepoContext repoContext, IReadOnlyList<string> projects, CancellationToken cancellationToken)
    {
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
        yield return fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, "src", "Directory.Build.props"), prefix: "Build Props");
        yield return fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, "src", "CodeAnalysis.ruleset"), prefix: "Build Props");

        if (fileContext.RepoContext.ClonePath.Contains(value: "funfair", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            yield return fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, "src", "FunFair.props"), prefix: "Build Props");
            yield return fileContext.MakeFile(string.Join(separator: Path.DirectorySeparatorChar, "src", "packageicon.png"), prefix: "Package Icon");
        }
    }

    private async Task<int> UpdateResharperSettingsAsync(RepoContext repoContext, TemplateUpdateContext updateContext, IReadOnlyList<string> solutions, CancellationToken cancellationToken)
    {
        const string dotSettingsExtension = ".DotSettings";

        string templateSourceDirectory = Path.Combine(path1: updateContext.TemplateFolder, path2: "src");
        string? dotSettingsSourceFile = Directory.GetFiles(path: templateSourceDirectory, "*.sln" + dotSettingsExtension)
                                                 .FirstOrDefault();

        if (string.IsNullOrEmpty(dotSettingsSourceFile))
        {
            return 0;
        }

        int changes = 0;

        foreach (string targetSolutionFileName in solutions)
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
                                                        string sourceDirectory,
                                                        IReadOnlyList<string> solutions,
                                                        IReadOnlyList<string> projects,
                                                        BuildSettings buildSettings,
                                                        CancellationToken cancellationToken)
    {
        string templateGlobalJsonFileName = Path.Combine(path1: updateContext.TemplateFolder, path2: "src", path3: "global.json");
        string targetGlobalJsonFileName = Path.Combine(path1: sourceDirectory, path2: "global.json");

        const string messagePrefix = "SDK - Updated DotNet SDK to ";
        string message = messagePrefix + updateContext.DotNetSettings.SdkVersion;

        const string branchPrefix = "depends/dotnet/sdk/";
        string branchName = branchPrefix + updateContext.DotNetSettings.SdkVersion;
        string invalidBranchName = branchPrefix + Guid.NewGuid();

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
                    await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
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
            await repoContext.Repository.ResetToMasterAsync(upstream: GitConstants.Upstream, cancellationToken: cancellationToken);
        }

        async ValueTask ChangelogUpdateAsync(CancellationToken token)
        {
            await ChangeLogUpdater.RemoveEntryAsync(changeLogFileName: repoContext.ChangeLogFileName, type: CHANGELOG_ENTRY_TYPE, message: messagePrefix, cancellationToken: token);
            await ChangeLogUpdater.AddEntryAsync(changeLogFileName: repoContext.ChangeLogFileName, type: CHANGELOG_ENTRY_TYPE, message: message, cancellationToken: token);

            bool ok = projects is [] || await this.CheckBuildAsync(updateContext: updateContext,
                                                                   sourceDirectory: sourceDirectory,
                                                                   solutions: solutions,
                                                                   buildSettings: buildSettings,
                                                                   cancellationToken: cancellationToken);

            if (!ok)
            {
                if (repoContext.Repository.DoesBranchExist(branchName))
                {
                    throw new BranchAlreadyExistsException(branchName);
                }

                await repoContext.Repository.CreateBranchAsync(branchName: branchName, cancellationToken: cancellationToken);

                branchCreated = true;
            }
        }

        bool GlobalSdkVersionCheck(byte[] source, byte[] target)
        {
            if (buildSettings.Framework is null)
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

            NuGetVersion targetVersion = new(buildSettings.Framework);
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

    private async Task<bool> CheckBuildAsync(TemplateUpdateContext updateContext,
                                             string sourceDirectory,
                                             IReadOnlyList<string> solutions,
                                             BuildSettings buildSettings,
                                             CancellationToken cancellationToken)
    {
        try
        {
            await this._dotNetSolutionCheck.PreCheckAsync(solutions: solutions, dotNetSettings: updateContext.DotNetSettings, buildSettings: buildSettings, cancellationToken: cancellationToken);

            await this._dotNetBuild.BuildAsync(basePath: sourceDirectory, buildSettings: buildSettings, cancellationToken: cancellationToken);

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

        return new(WorkFolder: workFolder, TemplateFolder: templateRepo.WorkingDirectory, TrackingFileName: trackingFileName, DotNetSettings: dotNetSettings, ReleaseConfig: releaseConfig);
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