using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Build.Services.LoggingExtensions;

internal static partial class DotNetBuildLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Building {basePath}...")]
    public static partial void LogStartingBuild(this ILogger<DotNetBuild> logger, string basePath);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Found framework {framework}")]
    public static partial void LogFoundFramework(this ILogger<DotNetBuild> logger, string framework);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Publishing no framework")]
    public static partial void LogPublishingNoFramework(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Publishing for {framework}")]
    public static partial void LogPublishingWithFramework(this ILogger<DotNetBuild> logger, string framework);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Packing...")]
    public static partial void LogPacking(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Testing...")]
    public static partial void LogTesting(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Building...")]
    public static partial void LogBuilding(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Restoring...")]
    public static partial void LogRestoring(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Cleaning...")]
    public static partial void LogCleaning(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Stopping build server...")]
    public static partial void LogStoppingBuildServer(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Stopped build server")]
    public static partial void LogStoppedBuildServer(this ILogger<DotNetBuild> logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "{message}")]
    public static partial void LogBuildError(this ILogger<DotNetBuild> logger, string message);
}