using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class FileUpdater : IFileUpdater
{
    private readonly ILogger<FileUpdater> _logger;

    public FileUpdater(ILogger<FileUpdater> logger)
    {
        this._logger = logger;
    }

    public ValueTask<bool> UpdateFileAsync(RepoContext repoContext, CopyInstruction copyInstruction, Func<CancellationToken, ValueTask> changelogUpdate, CancellationToken cancellationToken)
    {
        return this.UpdateFileAsync(repoContext: repoContext,
                                    templateSourceFileName: copyInstruction.SourceFileName,
                                    targetFileName: copyInstruction.TargetFileName,
                                    applyChanges: copyInstruction.Apply,
                                    commitMessage: copyInstruction.Message,
                                    changelogUpdate: changelogUpdate,
                                    cancellationToken: cancellationToken);
    }

    public async ValueTask<bool> UpdateFileAsync(RepoContext repoContext,
                                                 string templateSourceFileName,
                                                 string targetFileName,
                                                 Func<byte[], (byte[] bytes, bool changed)> applyChanges,
                                                 string commitMessage,
                                                 Func<CancellationToken, ValueTask> changelogUpdate,
                                                 CancellationToken cancellationToken)
    {
        this._logger.LogInformation($"Checking: {targetFileName} <-> {templateSourceFileName}");

        Difference diff = await this.IsSameContentAsync(sourceFileName: templateSourceFileName, targetFileName: targetFileName, applyChanges: applyChanges, cancellationToken: cancellationToken);

        return diff switch
        {
            Difference.TARGET_MISSING or Difference.DIFFERENT => await this.ReplaceFileAsync(repoContext: repoContext,
                                                                                             templateGlobalJsonFileName: templateSourceFileName,
                                                                                             targetGlobalJsonFileName: targetFileName,
                                                                                             commitMessage: commitMessage,
                                                                                             changelogUpdate: changelogUpdate,
                                                                                             cancellationToken: cancellationToken),
            _ => AlreadyUpToDate()
        };

        bool AlreadyUpToDate()
        {
            this._logger.LogInformation($"{targetFileName} is up to date with {templateSourceFileName}");

            return false;
        }
    }

    private async ValueTask<Difference> IsSameContentAsync(string sourceFileName, string targetFileName, Func<byte[], (byte[] bytes, bool changed)> applyChanges, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceFileName))
        {
            this._logger.LogDebug($"{sourceFileName} is missing");

            return Difference.SOURCE_MISSING;
        }

        byte[] sourceBytes = await File.ReadAllBytesAsync(path: sourceFileName, cancellationToken: cancellationToken);
        (sourceBytes, bool changed) = applyChanges(sourceBytes);

        if (changed)
        {
            this._logger.LogInformation($"Transform on {sourceFileName} resulted in changes");
        }

        if (!File.Exists(targetFileName))
        {
            this._logger.LogDebug($"{targetFileName} is missing");

            await File.WriteAllBytesAsync(path: targetFileName, bytes: sourceBytes, cancellationToken: cancellationToken);

            return Difference.TARGET_MISSING;
        }

        byte[] targetBytes = await File.ReadAllBytesAsync(path: targetFileName, cancellationToken: cancellationToken);

        if (sourceBytes.SequenceEqual(targetBytes))
        {
            this._logger.LogInformation($"{targetFileName} is unchanged");

            return Difference.SAME;
        }

        this._logger.LogInformation($"{targetFileName} is different");

        await File.WriteAllBytesAsync(path: targetFileName, bytes: sourceBytes, cancellationToken: cancellationToken);

        return Difference.DIFFERENT;
    }

    private async ValueTask<bool> ReplaceFileAsync(RepoContext repoContext,
                                                   string templateGlobalJsonFileName,
                                                   string targetGlobalJsonFileName,
                                                   string commitMessage,
                                                   Func<CancellationToken, ValueTask> changelogUpdate,
                                                   CancellationToken cancellationToken)
    {
        this._logger.LogInformation($"Updating {targetGlobalJsonFileName} from {templateGlobalJsonFileName} -> {commitMessage}");

        await changelogUpdate(cancellationToken);
        await repoContext.Repository.CommitAsync(message: commitMessage, cancellationToken: cancellationToken);

        return true;
    }
}