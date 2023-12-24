using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Credfeto.DotNet.Repo.Git;

public interface IGitRepository : IDisposable
{
    string WorkingDirectory { get; }

    string ClonePath { get; }

    Repository Active { get; }

    string HeadRev { get; }

    bool HasSubmodules { get; }

    void RemoveAllLocalBranches();

    ValueTask ResetToMasterAsync(string upstream, CancellationToken cancellationToken);

    string GetDefaultBranch(string upstream);

    bool HasUncommittedChanges();

    IReadOnlyCollection<string> GetRemoteBranches(string upstream);

    ValueTask CommitAsync(string message, CancellationToken cancellationToken);

    ValueTask CommitNamedAsync(string message, IReadOnlyList<string> files, CancellationToken cancellationToken);

    ValueTask PushAsync(CancellationToken cancellationToken);

    ValueTask PushOriginAsync(string branchName, string upstream, CancellationToken cancellationToken);

    bool DoesBranchExist(string branchName);

    void CreateBranch(string branchName);

    ValueTask RemoveBranchesForPrefixAsync(string branchForUpdate, string branchPrefix, string upstream, CancellationToken cancellationToken);

    DateTimeOffset GetLastCommitDate();
}