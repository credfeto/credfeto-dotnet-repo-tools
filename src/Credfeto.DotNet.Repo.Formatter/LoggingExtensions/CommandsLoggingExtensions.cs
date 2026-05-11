using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Formatter.LoggingExtensions;

internal static partial class CommandsLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Processing file: {fileName}"
    )]
    public static partial void LogProcessingFile(this ILogger logger, string fileName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "File unchanged: {fileName}"
    )]
    public static partial void LogFileUnchanged(this ILogger logger, string fileName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "File updated: {fileName}")]
    public static partial void LogFileUpdated(this ILogger logger, string fileName);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Invalid file type: {fileName} (only .cs and .csproj files are supported)"
    )]
    public static partial void LogInvalidFileType(this ILogger logger, string fileName);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "--build-root must be specified when --remove-suppressions is enabled"
    )]
    public static partial void LogBuildRootRequired(this ILogger logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Build root does not exist: {buildRoot}"
    )]
    public static partial void LogBuildRootNotFound(this ILogger logger, string buildRoot);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "No input files specified")]
    public static partial void LogNoInputFiles(this ILogger logger);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Completed. {fileCount} file(s) processed, {updatedCount} updated."
    )]
    public static partial void LogCompleted(this ILogger logger, int fileCount, int updatedCount);
}
