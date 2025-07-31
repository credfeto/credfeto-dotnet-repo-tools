using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;

internal static partial class DependencyReducerLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Number of projects to check: {projectCount}")]
    public static partial void ProjectsToCheck(this ILogger<DependencyReducer> logger, int projectCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Checking Projects")]
    public static partial void CheckingProjects(this ILogger<DependencyReducer> logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "({projectInstance}/{projectCount}): Testing project: {project}")]
    public static partial void StartTestingProject(this ILogger<DependencyReducer> logger, int projectInstance, int projectCount, string project);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "* Does not build without changes")]
    public static partial void DoesNotBuildWithoutChanges(this ILogger<DependencyReducer> logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Finished Cheching Projects")]
    public static partial void FinishedCheckingProjects(this ILogger<DependencyReducer> logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "* Building {project} using {minimalSdk} instead of {currentSdk}...")]
    public static partial void BuildingProjectWithMinimalSdk(this ILogger<DependencyReducer> logger, string project, string minimalSdk, string currentSdk);
}