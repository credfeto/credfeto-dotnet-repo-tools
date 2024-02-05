using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using Credfeto.Extensions.Linq;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Services;

public sealed class DotNetVersion : IDotNetVersion
{
    public async ValueTask<IReadOnlyList<Version>> GetInstalledSdksAsync(CancellationToken cancellationToken)
    {
        (string[] output, int exitCode) = await ExecAsync(arguments: "--list-sdks", cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            throw new InvalidOperationException("Failed to list installed SDKs");
        }

        return
        [
            ..output.Select(ExtractVersion)
                    .RemoveNulls()
                    .OrderByDescending(x => x)
        ];
    }

    private static Version? ExtractVersion(string line)
    {
        string[] parts = line.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return null;
        }

        return Version.TryParse(parts[0], out Version? version)
            ? version
            : null;
    }

    private static async ValueTask<(string[] Output, int ExitCode)> ExecAsync(string arguments, CancellationToken cancellationToken)
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
                                       ["MSBUILDTERMINALLOGGER"] = "true"
                                   }
                               };

        using (Process? process = Process.Start(psi))
        {
            if (process is null)
            {
                throw new InvalidOperationException("Failed to start git");
            }

#if NET7_0_OR_GREATER
            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);

            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
#else
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
#endif

            await process.WaitForExitAsync(cancellationToken);

            string result = string.Join(separator: Environment.NewLine, output, error);

            return (result.Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries), process.ExitCode);
        }
    }
}