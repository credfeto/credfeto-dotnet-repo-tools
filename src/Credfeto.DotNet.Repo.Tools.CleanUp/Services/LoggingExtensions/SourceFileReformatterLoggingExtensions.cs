using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;

internal static partial class SourceFileReformatterLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Cleaning: {filename} Errors occured: {errors}")]
    private static partial void FormattingErrorsFound(this ILogger<SourceFileReformatter> logger, string filename, string errors);

    public static void FormattingErrorsFound(this ILogger<SourceFileReformatter> logger, string filename, IReadOnlyList<Diagnostic> errors)
    {
        string msg = string.Join(separator: Environment.NewLine, errors.Select(e => $"{e.Id}: {e.Location}: {e.GetMessage()}"));
        logger.FormattingErrorsFound(filename: filename, errors: msg);
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Cleaning: {filename} Errors occured: {message}")]
    public static partial void FormattingErrorsFound(this ILogger<SourceFileReformatter> logger, string filename, string message, Exception exception);
}