using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Extensions;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Services;

public sealed class DotNetCommandRunner : IDotNetCommandRunner
{
    [SuppressMessage(
        category: "SonarAnalyzer.CSharp",
        checkId: "S4036: Use an absolute path for this command",
        Justification = "Relies on dotnet being resolved via PATH"
    )]
    public ValueTask<(string[] Output, int ExitCode)> RunAsync(string arguments, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["DOTNET_NOLOGO"] = "true",
                ["DOTNET_PRINT_TELEMETRY_MESSAGE"] = "0",
                ["DOTNET_ReadyToRun"] = "0",
                ["DOTNET_TC_QuickJitForLoops"] = "1",
                ["DOTNET_TieredPGO"] = "1",
                ["MSBUILDTERMINALLOGGER"] = "false",
            },
        };

        return ProcessRunner.ExecAsync(
            psi: psi,
            failedToStartMessage: "Failed to start dotnet",
            cancellationToken: cancellationToken
        );
    }
}
