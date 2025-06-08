using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;

internal static partial class TemplateConfigLoaderLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Loading template configuration from {source}...")]
    public static partial void LoadingTemplateConfig(this ILogger<TemplateConfigLoader> logger, string source);
}