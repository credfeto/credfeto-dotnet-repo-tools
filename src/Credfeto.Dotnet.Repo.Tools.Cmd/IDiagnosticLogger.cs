using Microsoft.Extensions.Logging;

namespace Credfeto.Dotnet.Repo.Tools.Cmd;

public interface IDiagnosticLogger : ILogger
{
    long Errors { get; }

    bool IsErrored { get; }
}