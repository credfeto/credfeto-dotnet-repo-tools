using System.Diagnostics;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Models;

[DebuggerDisplay("Repo: {ClonePath} File: {ChangeLogFileName}")]
public readonly record struct RepoContext(string ClonePath, IGitRepository Repository, string WorkingDirectory, string ChangeLogFileName)
{
    public RepoContext(IGitRepository Repository, string ChangeLogFileName)
        : this(ClonePath: Repository.ClonePath, Repository: Repository, WorkingDirectory: Repository.WorkingDirectory, ChangeLogFileName: ChangeLogFileName)
    {
    }
}