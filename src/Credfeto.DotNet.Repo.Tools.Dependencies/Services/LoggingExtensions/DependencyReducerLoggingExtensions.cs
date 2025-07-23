using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;

internal static partial class DependencyReducerLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Number of projects to check: {projectCount}")]
    public static partial void ProjectsToCheck(this ILogger<DependencyReducer> logger, int projectCount);
}