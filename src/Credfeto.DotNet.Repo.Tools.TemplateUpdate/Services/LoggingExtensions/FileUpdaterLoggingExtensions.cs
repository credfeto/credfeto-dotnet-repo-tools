using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;

internal static partial class FileUpdaterLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Checking: {sourceFileName} <-> {targetFileName}")]
    private static partial void LogCheckingFile(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    public static void LogCheckingFile(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogCheckingFile(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }
}