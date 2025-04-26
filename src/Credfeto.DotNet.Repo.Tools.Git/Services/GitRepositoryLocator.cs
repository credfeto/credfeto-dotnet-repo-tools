using System;
using System.IO;
using System.Linq;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Git.Services;

public sealed class GitRepositoryLocator : IGitRepositoryLocator
{
    private const string GIT_SUFFIX = ".git";

    public string GetWorkingDirectory(string workDir, string repoUrl)
    {
        string work = CleanupRepoUrl(repoUrl);

        string[] folders = SplitToFolders(work);

        return Path.Combine(path1: workDir, folders[^2], folders[^1]);
    }

    private static string CleanupRepoUrl(string repoUrl)
    {
        string work = repoUrl.TrimEnd('/');

        if (work.EndsWith(value: GIT_SUFFIX, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return work[..^GIT_SUFFIX.Length];
        }

        return work;
    }

    private static string[] SplitToFolders(string work)
    {
        return
        [
            .. work.Split(separator: '/', options: StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(item => item.Split(separator: ':', options: StringSplitOptions.RemoveEmptyEntries)),
        ];
    }
}
