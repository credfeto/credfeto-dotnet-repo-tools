using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Extensions;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;

namespace Credfeto.DotNet.Repo.Tools.Git.Helpers;

public static class GitCommandLine
{
    [SuppressMessage(
        category: "SonarAnalyzer.CSharp",
        checkId: "S4036: Use an absolute path for this command",
        Justification = "Relies on git being resolved via PATH"
    )]
    public static ValueTask<(string[] Output, int ExitCode)> ExecAsync(
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

        return ProcessRunner.ExecAsync(
            psi: psi,
            failedToStartMessage: "Failed to start git",
            cancellationToken: cancellationToken
        );
    }

    private static void EnsureNotLocked(string repoUrl, string workingDirectory)
    {
        string gitFolder = Path.Combine(path1: workingDirectory, path2: ".git");

        if (!Directory.Exists(gitFolder))
        {
            return;
        }

        IReadOnlyList<string> lockFiles = [.. LockFiles(gitFolder).Where(File.Exists).Order(StringComparer.Ordinal)];

        if (lockFiles is [])
        {
            return;
        }

        throw new GitRepositoryLockedException(
            $"Repository {repoUrl} at {workingDirectory} is locked ({string.Join(", ", lockFiles)})."
        );
    }

    private static IEnumerable<string> LockFiles(string dotGitDirectory)
    {
        string objectsPath = Path.Combine(path1: dotGitDirectory, path2: "objects") + Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(dotGitDirectory, "*.lock", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(value: objectsPath, comparisonType: StringComparison.Ordinal));
    }
}
