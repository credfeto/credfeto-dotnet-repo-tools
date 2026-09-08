using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.DotNet.Repo.Tools.Extensions;

public static class ProcessRunner
{
    public static async ValueTask<(string[] Output, int ExitCode)> ExecAsync(
        ProcessStartInfo psi,
        string failedToStartMessage,
        CancellationToken cancellationToken
    )
    {
        using Process process = Process.Start(psi) ?? throw new InvalidOperationException(failedToStartMessage);

        try
        {
            string[] streams = await Task.WhenAll(
                process.StandardOutput.ReadToEndAsync(cancellationToken),
                process.StandardError.ReadToEndAsync(cancellationToken)
            );

            await process.WaitForExitAsync(cancellationToken);

            string[] outputLines = streams[0]
                .Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries);
            string[] errorLines = streams[1]
                .Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries);

            return ([.. outputLines, .. errorLines], process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited between the HasExited check and Kill().
                Debug.Assert(
                    condition: process.HasExited,
                    message: "Kill() only throws InvalidOperationException once the process has exited."
                );
            }

            throw;
        }
    }
}
