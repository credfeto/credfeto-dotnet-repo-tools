using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Services;

internal sealed class DotNetCommandRunner : IDotNetCommandRunner
{
    public async ValueTask<(string[] Output, int ExitCode)> RunAsync(
        string arguments,
        CancellationToken cancellationToken
    )
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

        // ! Process.Start with UseShellExecute=false never returns null
        using Process process = Process.Start(psi)!;

#if NET7_0_OR_GREATER
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);
#else
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
#endif

        await process.WaitForExitAsync(cancellationToken);

        string result = string.Join(separator: Environment.NewLine, output, error);

        return (
            result.Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries),
            process.ExitCode
        );
    }
}
