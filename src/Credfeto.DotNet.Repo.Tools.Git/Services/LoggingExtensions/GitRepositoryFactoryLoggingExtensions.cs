using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Git.Services.LoggingExtensions;

internal static partial class GitRepositoryFactoryLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Opening {repoUrl} at {repoPath}")]
    public static partial void OpeningRepo(this ILogger<GitRepositoryFactory> logger, string repoUrl, string repoPath);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Cloning {repoUrl} into {repoPath}")]
    public static partial void CloningRepo(this ILogger<GitRepositoryFactory> logger, string repoUrl, string repoPath);
}