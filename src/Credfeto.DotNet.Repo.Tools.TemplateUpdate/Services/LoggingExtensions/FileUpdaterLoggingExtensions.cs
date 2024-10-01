using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;

internal static partial class FileUpdaterLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Checking: {sourceFileName} <-> {targetFileName}")]
    private static partial void LogCheckingFile(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "{targetFileName} is already up to date with {sourceFileName}")]
    private static partial void LogAlreadyUpToDate(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "{sourceFileName} was transformed before compare")]
    [Conditional("DEBUG")]
    private static partial void LogSourceTransformed(this ILogger<FileUpdater> logger, string sourceFileName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "{targetFileName} is different to {sourceFileName}, updating")]
    [Conditional("DEBUG")]
    private static partial void LogTargetDifferent(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "{targetFileName} is the same as {sourceFileName}")]
    [Conditional("DEBUG")]
    private static partial void LogTargetIdenticalToSource(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "{targetFileName} missing, copying from {sourceFileName}")]
    [Conditional("DEBUG")]
    private static partial void LogTargetMissing(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "{sourceFileName} is missing, skipping update")]
    private static partial void LogSourceMissing(this ILogger<FileUpdater> logger, string sourceFileName);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Committing {targetFileName} : {message}")]
    private static partial void LogCommitting(this ILogger<FileUpdater> logger, string targetFileName, string message);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "{targetFileName} is the same as {sourceFileName}")]
    [Conditional("DEBUG")]
    private static partial void LogTargetNewerThanSource(this ILogger<FileUpdater> logger, string sourceFileName, string targetFileName);

    public static void LogCheckingFile(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogCheckingFile(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }

    public static void LogAlreadyUpToDate(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogAlreadyUpToDate(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }

    public static void LogSourceTransformed(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogSourceTransformed(sourceFileName: copyInstruction.SourceFileName);
    }

    public static void LogTargetDifferent(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogTargetDifferent(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }

    public static void LogTargetIdenticalToSource(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogTargetIdenticalToSource(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }

    public static void LogTargetNewerThanSource(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogTargetNewerThanSource(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }

    public static void LogTargetMissing(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogTargetMissing(sourceFileName: copyInstruction.SourceFileName, targetFileName: copyInstruction.TargetFileName);
    }

    public static void LogSourceMissing(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogSourceMissing(sourceFileName: copyInstruction.SourceFileName);
    }

    public static void LogCommitting(this ILogger<FileUpdater> logger, in CopyInstruction copyInstruction)
    {
        logger.LogCommitting(targetFileName: copyInstruction.TargetFileName, message: copyInstruction.Message);
    }
}