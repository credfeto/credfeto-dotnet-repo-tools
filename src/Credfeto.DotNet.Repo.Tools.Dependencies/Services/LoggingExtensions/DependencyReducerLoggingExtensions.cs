using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Services.LoggingExtensions;

internal static partial class DependencyReducerLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Number of projects to check: {projectCount}")]
    public static partial void ProjectsToCheck(this ILogger<DependencyReducer> logger, int projectCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Checking Projects")]
    public static partial void CheckingProjects(this ILogger<DependencyReducer> logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "({projectInstance}/{projectCount}): Starting project testing: {project}"
    )]
    public static partial void StartTestingProject(
        this ILogger<DependencyReducer> logger,
        int projectInstance,
        int projectCount,
        string project
    );

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "({projectInstance}/{projectCount}): Completed project testing: {project}"
    )]
    public static partial void FinishTestingProject(
        this ILogger<DependencyReducer> logger,
        int projectInstance,
        int projectCount,
        string project
    );

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "* Does not build without changes")]
    public static partial void DoesNotBuildWithoutChanges(this ILogger<DependencyReducer> logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Finished Cheching Projects")]
    public static partial void FinishedCheckingProjects(this ILogger<DependencyReducer> logger);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "* Building {project} using {minimalSdk} instead of {currentSdk}..."
    )]
    public static partial void BuildingProjectWithMinimalSdk(
        this ILogger<DependencyReducer> logger,
        string project,
        string minimalSdk,
        string currentSdk
    );

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "### Failed to build {project} after restore.")]
    public static partial void FailedToBuildProjectAfterRestore(this ILogger<DependencyReducer> logger, string project);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "= Skipping {packageId} as it is marked as do not remove"
    )]
    public static partial void SkippingDoNotRemovePackage(this ILogger<DependencyReducer> logger, string packageId);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Checking {packageId} ({version})")]
    public static partial void CheckingPackage(
        this ILogger<DependencyReducer> logger,
        string packageId,
        string version
    );

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Information,
        Message = "= {project} references package {packageId} ({version}) also in child project {childProject}"
    )]
    public static partial void ChildProjectReferencesPackage(
        this ILogger<DependencyReducer> logger,
        string project,
        string packageId,
        string version,
        string childProject
    );

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "* Building {project} without package {packageId} ({version})..."
    )]
    public static partial void BuildingProjectWithoutPackage(
        this ILogger<DependencyReducer> logger,
        string project,
        string packageId,
        string version
    );

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Checking {relativeInclude}")]
    public static partial void CheckingProjectReference(this ILogger<DependencyReducer> logger, string relativeInclude);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Information,
        Message = "= {project} references package {relativeInclude} also in child project {childProject}"
    )]
    public static partial void ChildProjectReferencesProject(
        this ILogger<DependencyReducer> logger,
        string project,
        string relativeInclude,
        string childProject
    );

    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Information,
        Message = "* Building {project} without project reference {relativeInclude}..."
    )]
    public static partial void BuildingProjectWithoutProject(
        this ILogger<DependencyReducer> logger,
        string project,
        string relativeInclude
    );

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Information,
        Message = "= {project}: SDK does not need changing. Currently {sdk}."
    )]
    public static partial void SdkDoesNotNeedChanging(
        this ILogger<DependencyReducer> logger,
        string project,
        string sdk
    );

    [LoggerMessage(
        EventId = 17,
        Level = LogLevel.Information,
        Message = "Analyse completed in {durationSeconds} seconds"
    )]
    public static partial void AnalyzeCompletedInDuration(
        this ILogger<DependencyReducer> logger,
        double durationSeconds
    );

    [LoggerMessage(
        EventId = 18,
        Level = LogLevel.Information,
        Message = "{count} SDK reference(s) could potentially be narrowed."
    )]
    public static partial void SdkNarrowReferenceCount(this ILogger<DependencyReducer> logger, long count);

    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Information,
        Message = "{count} reference(s) could potentially be removed"
    )]
    public static partial void ReferencesCouldBeRemoved(this ILogger<DependencyReducer> logger, long count);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "{count} reference(s) could potentially be switched"
    )]
    public static partial void ReferencesCouldBeSwitched(this ILogger<DependencyReducer> logger, long count);

    [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "{section}: {value}")]
    public static partial void WriteStatistics(this ILogger<DependencyReducer> logger, string section, long value);

    [LoggerMessage(EventId = 22, Level = LogLevel.Information, Message = "{section}")]
    public static partial void LogSection(this ILogger<DependencyReducer> logger, string section);

    [LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "Project References: {project}")]
    public static partial void LogProject(this ILogger<DependencyReducer> logger, string project);

    [LoggerMessage(
        EventId = 24,
        Level = LogLevel.Information,
        Message = "{project}: check removal of {packageId} ({version})"
    )]
    public static partial void ProjectPackageReference(
        this ILogger<DependencyReducer> logger,
        string project,
        string packageId,
        string? version
    );

    [LoggerMessage(
        EventId = 25,
        Level = LogLevel.Information,
        Message = "{project}: check removal of project {referenceProject}"
    )]
    public static partial void ProjectChildProjectReference(
        this ILogger<DependencyReducer> logger,
        string project,
        string referenceProject
    );

    [LoggerMessage(EventId = 26, Level = LogLevel.Information, Message = "{project}: check narrowing of {sdk}")]
    public static partial void ProjectSdkReference(this ILogger<DependencyReducer> logger, string project, string sdk);

    [LoggerMessage(EventId = 27, Level = LogLevel.Information, Message = "{project}: check unknown {context}")]
    public static partial void UnknownReference(this ILogger<DependencyReducer> logger, string project, string context);
}
