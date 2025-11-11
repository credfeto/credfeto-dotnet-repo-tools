using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;

namespace Credfeto.DotNet.Repo.Tools.Git.Helpers;

internal static class GitCommandLine
{
    public static async ValueTask<(string[] Output, int ExitCode)> ExecAsync(
        string clonePath,
        string repoPath,
        string arguments,
        CancellationToken cancellationToken
    )
    {
        EnsureNotLocked(repoUrl: clonePath, workingDirectory: repoPath);

        ProcessStartInfo psi = new()
        {
            FileName = "git",
            WorkingDirectory = repoPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["GIT_REDIRECT_STDERR"] = "2>&1" },
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

            return (
                result.Split(separator: Environment.NewLine, options: StringSplitOptions.RemoveEmptyEntries),
                process.ExitCode
            );
        }
    }

    public static void EnsureNotLocked(string repoUrl, string workingDirectory)
    {
        IReadOnlyList<string> lockFiles =
        [
            .. LockFiles(Path.Combine(path1: workingDirectory, path2: ".git"))
               .Where(File.Exists)
               .Order(StringComparer.Ordinal)
        ];

        if (lockFiles is [])
        {
            return;
        }

        throw new GitRepositoryLockedException($"Repository {repoUrl} at {workingDirectory} is locked ({string.Join(", ", lockFiles)}).");
    }

    private static IEnumerable<string> LockFiles(string dotGitDirectory)
    {
        return Directory.EnumerateFiles(dotGitDirectory, "*.lock", SearchOption.AllDirectories);
    }
}
