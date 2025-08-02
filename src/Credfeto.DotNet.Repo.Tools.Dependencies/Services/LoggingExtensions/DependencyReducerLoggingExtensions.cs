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

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "### Failed to build {project} after restore.")]
    public static partial void FailedToBuildProjectAfterRestore(this ILogger<DependencyReducer> logger, string project);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "= Skipping {packageId} as it is marked as do not remove")]
    public static partial void SkippingDoNotRemovePackage(this ILogger<DependencyReducer> logger, string packageId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Checking {packageId} ({version})")]
    public static partial void CheckingPackage(this ILogger<DependencyReducer> logger, string packageId, string version);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "= {project} references package {packageId} ({version}) also in child project {childProject}")]
    public static partial void ChildProjectReferencesPackage(this ILogger<DependencyReducer> logger, string project, string packageId, string version, string childProject);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "* Building {project} without package {packageId} ({version})...")]
    public static partial void BuildingProjectWithoutPackage(this ILogger<DependencyReducer> logger, string project, string packageId, string version);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Checking {relativeInclude}")]
    public static partial void CheckingProjectReference(this ILogger<DependencyReducer> logger, string relativeInclude);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "= {project} references package {relativeInclude} also in child project {childProject}")]
    public static partial void ChildProjectReferencesProject(this ILogger<DependencyReducer> logger, string project, string relativeInclude, string childProject);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "* Building {project} without project reference {relativeInclude}...")]
    public static partial void BuildingProjectWithoutProject(this ILogger<DependencyReducer> logger, string project, string relativeInclude);
}