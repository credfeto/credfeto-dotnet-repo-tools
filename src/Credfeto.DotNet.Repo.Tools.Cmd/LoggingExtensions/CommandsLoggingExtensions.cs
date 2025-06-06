using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Cmd.LoggingExtensions;

internal static partial class CommandsLoggingExtensions
{
    [Conditional("DEBUG")]
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "* {repo}")]
    public static partial void LogRepo(this ILogger<Commands> logger, string repo);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Done")]
    public static partial void LogCompleted(this ILogger<Commands> logger);
}