using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class FileUpdater : IFileUpdater
{
    private readonly ILogger<FileUpdater> _logger;

    public FileUpdater(ILogger<FileUpdater> logger)
    {
        this._logger = logger;
    }

    public async ValueTask<bool> UpdateFileAsync(RepoContext repoContext, CopyInstruction copyInstruction, Func<CancellationToken, ValueTask> changelogUpdate, CancellationToken cancellationToken)
    {
        this._logger.LogCheckingFile(copyInstruction);

        Difference diff = await this.AttemptToUpdateAsync(copyInstruction: copyInstruction, cancellationToken: cancellationToken);

        return diff switch
        {
            Difference.TARGET_MISSING or Difference.DIFFERENT => await this.CommitFileAsync(
                repoContext: repoContext,
                copyInstruction: copyInstruction,
                changelogUpdate: changelogUpdate,
                cancellationToken: cancellationToken
            ),
            _ => this.AlreadyUpToDate(copyInstruction),
        };
    }

    private bool AlreadyUpToDate(in CopyInstruction copyInstruction)
    {
        this._logger.LogAlreadyUpToDate(copyInstruction);

        return false;
    }

    private async ValueTask<Difference> AttemptToUpdateAsync(CopyInstruction copyInstruction, CancellationToken cancellationToken)
    {
        if (!File.Exists(copyInstruction.SourceFileName))
        {
            return this.OnSourceMissing(copyInstruction);
        }

        byte[] sourceBytes = await this.ReadSourceFileAsync(copyInstruction: copyInstruction, cancellationToken: cancellationToken);

        EnsureFolderExistsForFile(copyInstruction);

        if (!File.Exists(copyInstruction.TargetFileName))
        {
            return await this.OnTargetMissingAsync(copyInstruction: copyInstruction, sourceBytes: sourceBytes, cancellationToken: cancellationToken);
        }

        byte[] targetBytes = await ReadTargetFileAsync(copyInstruction: copyInstruction, cancellationToken: cancellationToken);

        if (IsSameContent(sourceBytes: sourceBytes, targetBytes: targetBytes))
        {
            return this.OnContentUnchanged(copyInstruction);
        }

        if (copyInstruction.IsTargetNewer(arg1: sourceBytes, arg2: targetBytes))
        {
            return this.OnContentTargetNewer(copyInstruction);
        }

        return await this.OnContentDifferentAsync(copyInstruction: copyInstruction, sourceBytes: sourceBytes, cancellationToken: cancellationToken);
    }

    private static bool IsSameContent(byte[] sourceBytes, byte[] targetBytes)
    {
        return sourceBytes.SequenceEqual(targetBytes);
    }

    private static void EnsureFolderExistsForFile(in CopyInstruction copyInstruction)
    {
        string? parent = Path.GetDirectoryName(copyInstruction.TargetFileName);

        if (parent is not null && !Directory.Exists(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private static async ValueTask<byte[]> ReadTargetFileAsync(CopyInstruction copyInstruction, CancellationToken cancellationToken)
    {
        return await File.ReadAllBytesAsync(path: copyInstruction.TargetFileName, cancellationToken: cancellationToken);
    }

    private async ValueTask<byte[]> ReadSourceFileAsync(CopyInstruction copyInstruction, CancellationToken cancellationToken)
    {
        byte[] sourceBytes = await File.ReadAllBytesAsync(path: copyInstruction.SourceFileName, cancellationToken: cancellationToken);
        (sourceBytes, bool changed) = copyInstruction.Apply(sourceBytes);

        if (changed)
        {
            this._logger.LogSourceTransformed(copyInstruction);
        }

        return sourceBytes;
    }

    private async ValueTask<Difference> OnContentDifferentAsync(CopyInstruction copyInstruction, byte[] sourceBytes, CancellationToken cancellationToken)
    {
        this._logger.LogTargetDifferent(copyInstruction);

        await WriteTargetFileAsync(copyInstruction: copyInstruction, sourceBytes: sourceBytes, cancellationToken: cancellationToken);

        return Difference.DIFFERENT;
    }

    private static Task WriteTargetFileAsync(in CopyInstruction copyInstruction, byte[] sourceBytes, in CancellationToken cancellationToken)
    {
        return File.WriteAllBytesAsync(path: copyInstruction.TargetFileName, bytes: sourceBytes, cancellationToken: cancellationToken);
    }

    private Difference OnContentUnchanged(in CopyInstruction copyInstruction)
    {
        this._logger.LogTargetIdenticalToSource(copyInstruction);

        return Difference.SAME;
    }

    private Difference OnContentTargetNewer(in CopyInstruction copyInstruction)
    {
        this._logger.LogTargetNewerThanSource(copyInstruction);

        return Difference.TARGET_NEWER;
    }

    private async ValueTask<Difference> OnTargetMissingAsync(CopyInstruction copyInstruction, byte[] sourceBytes, CancellationToken cancellationToken)
    {
        this._logger.LogTargetMissing(copyInstruction);

        await WriteTargetFileAsync(copyInstruction: copyInstruction, sourceBytes: sourceBytes, cancellationToken: cancellationToken);

        return Difference.TARGET_MISSING;
    }

    private Difference OnSourceMissing(in CopyInstruction copyInstruction)
    {
        this._logger.LogSourceMissing(copyInstruction);

        return Difference.SOURCE_MISSING;
    }

    private async ValueTask<bool> CommitFileAsync(RepoContext repoContext, CopyInstruction copyInstruction, Func<CancellationToken, ValueTask> changelogUpdate, CancellationToken cancellationToken)
    {
        this._logger.LogCommitting(copyInstruction);

        await changelogUpdate(cancellationToken);
        await repoContext.Repository.CommitAsync(message: copyInstruction.Message, cancellationToken: cancellationToken);

        return true;
    }
}
