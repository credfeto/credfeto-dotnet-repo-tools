using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Git.Services.LoggingExtensions;

internal static partial class GitRepositoryLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Pushed {canonicalName}")]
    public static partial void LogPushedBranch(this ILogger logger, string canonicalName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Pushed {canonicalName} to {upstream} ({branchName})")]
    public static partial void LogPushedBranchUpstream(this ILogger logger, string canonicalName, string upstream, string branchName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Deleting branch {branch} from local repo...")]
    public static partial void LogDeletingLocalBranch(this ILogger logger, string branch);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Deleting branch {branch} from upstream {upstream}...")]
    public static partial void LogDeletingUpstreamBranch(this ILogger logger, string branch, string upstream);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Deleting branch {branch} is not upstream {upstream}...")]
    public static partial void LogSkippingDeleteOfUpstreamBranch(this ILogger logger, string branch, string upstream);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "{prefix} exit code: {exitCode}")]
    public static partial void LogGitExitCode(this ILogger logger, string prefix, int exitCode);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "{message}")]
    public static partial void LogGitMessage(this ILogger logger, string message);
}