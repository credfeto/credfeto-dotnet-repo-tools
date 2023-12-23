using System.Diagnostics;
using LibGit2Sharp;

namespace Credfeto.DotNet.Repo.Tools.Cmd.Models;

[DebuggerDisplay("Repo: {ClonePath} File: {ChangeLogFileName}")]
public readonly record struct RepoContext(string ClonePath, Repository Repository, string ChangeLogFileName)
{
    public string WorkingDirectory()
    {
        return this.Repository.Info.WorkingDirectory;
    }
}