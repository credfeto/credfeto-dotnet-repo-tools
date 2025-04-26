using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;

internal static partial class SourceFileReformatterLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Cleaning: {filename} Errors occured")]
    public static partial void FormattingErrorsFound(this ILogger<SourceFileReformatter> logger, string filename);
}