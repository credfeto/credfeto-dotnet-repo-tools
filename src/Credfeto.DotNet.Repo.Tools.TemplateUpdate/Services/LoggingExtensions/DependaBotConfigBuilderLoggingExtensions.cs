using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;

internal static partial class DependaBotConfigBuilderLoggingExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Adding Dependabot config for {ecoSystem} in {directory}")]
    public static partial void LogAddingConfigForEcosystem(this ILogger<DependaBotConfigBuilder> logger, string ecoSystem, string directory);
}
