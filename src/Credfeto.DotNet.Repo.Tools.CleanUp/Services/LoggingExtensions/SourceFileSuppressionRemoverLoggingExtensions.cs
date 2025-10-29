using System;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;

internal static partial class SourceFileSuppressionRemoverLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Cleaning: {filename} Errors occured: {message}")]
    public static partial void FailedToBuild(this ILogger<SourceFileSuppressionRemover> logger, string filename, string message, Exception exception);
}