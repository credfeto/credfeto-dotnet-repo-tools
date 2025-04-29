using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;

internal static partial class ProjectXmlRewriterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Found Duplicate Property: {filename} {propertyName}"
    )]
    public static partial void DuplicateProperty(
        this ILogger<ProjectXmlRewriter> logger,
        string filename,
        string propertyName
    );

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "{filename} SKIPPING GROUP AS Found Comment")]
    public static partial void SkippingGroupWithComment(this ILogger<ProjectXmlRewriter> logger, string filename);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "{filename} SKIPPING GROUP AS Found Duplicate item {name}"
    )]
    public static partial void SkippingGroupWithDuplicate(
        this ILogger<ProjectXmlRewriter> logger,
        string filename,
        string name
    );

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "{filename} SKIPPING GROUP AS Found Attribute")]
    public static partial void SkippingGroupWithAttribute(this ILogger<ProjectXmlRewriter> logger, string filename);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "{filename} SKIPPING GROUP AS Found duplicate package {packageId}"
    )]
    public static partial void SkippingGroupWithDuplicatePackage(
        this ILogger<ProjectXmlRewriter> logger,
        string filename,
        string packageId
    );

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "{filename} SKIPPING GROUP AS Found duplicate project {projectPath}"
    )]
    public static partial void SkippingGroupWithDuplicateProject(
        this ILogger<ProjectXmlRewriter> logger,
        string filename,
        string projectPath
    );

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "{filename} SKIPPING GROUP AS Found unknown item type {referenceType}"
    )]
    public static partial void SkippingGroupWithUnknownItemType(
        this ILogger<ProjectXmlRewriter> logger,
        string filename,
        string referenceType
    );
}
