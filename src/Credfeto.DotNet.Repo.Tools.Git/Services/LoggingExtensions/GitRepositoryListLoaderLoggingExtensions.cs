using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Git.Services.LoggingExtensions;

internal static partial class GitRepositoryListLoaderLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Loading repository list from {source}...")]
    public static partial void LoadingRepos(this ILogger<GitRepositoryListLoader> logger, string source);
}
