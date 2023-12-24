using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Git.Exceptions;
using LibGit2Sharp;

namespace Credfeto.DotNet.Repo.Git;

internal sealed class GitRepository : IGitRepository
{
    private static readonly CheckoutOptions GitCheckoutOptions = new() { CheckoutModifiers = CheckoutModifiers.Force };

    private Repository? _repo;

    internal GitRepository(string clonePath, string workingDirectory, Repository? repo)
    {
        this.ClonePath = clonePath;
        this.WorkingDirectory = workingDirectory;
        this._repo = repo;
    }

    public Repository Active => this._repo ?? OpenRepository(workDir: this.WorkingDirectory);

    public string ClonePath { get; }

    public string WorkingDirectory { get; }

    public void Dispose()
    {
        this.ResetRepo();
    }

    public async ValueTask ResetToMasterAsync(string upstream, CancellationToken cancellationToken)
    {
        Remote remote = this.GetRemote(upstream);

        string defaultBranch = this.GetDefaultBranch(upstream: upstream);
        await this.FetchRemoteAsync(remote: remote, cancellationToken: cancellationToken);

        this.Active.Reset(resetMode: ResetMode.Hard, commit: this.Active.Head.Tip);

        await this.CleanRepoAsync(cancellationToken: cancellationToken);

        this.Active.Checkout(tree: this.Active.Branches[defaultBranch].Tip.Tree, paths: null, options: GitCheckoutOptions);

        this.Active.Reset(resetMode: ResetMode.Hard, commit: this.Active.Head.Tip);

        await this.CleanRepoAsync(cancellationToken: cancellationToken);

        // # NOTE Loses all local commits on master
        // & git -C $repoPath reset --hard $upstreamBranch 2>&1 | Out-Null
        this.Active.Reset(resetMode: ResetMode.Hard, commit: this.Active.Branches[upstream + "/" + defaultBranch].Tip);

        await this.FetchRemoteAsync(remote: remote, cancellationToken: cancellationToken);

        // & git -C $repoPath prune 2>&1 | Out-Null
        await this.PruneAsync(cancellationToken: cancellationToken);

        if (this.HasUncommittedChanges())
        {
            throw new GitException("Failed to reset to " + defaultBranch + " - uncommitted changes");
        }
    }

    public void RemoveAllLocalBranches()
    {
        Repository repo = this.Active;
        string headCanonicalName = repo.Head.CanonicalName;
        IReadOnlyList<Branch> branchesToRemove = [..repo.Branches.Where(b => IsLocalBranch(b) && !IsCurrentBranch(b))];

        foreach (Branch branch in branchesToRemove)
        {
            this.Active.Branches.Remove(branch);
        }

        static bool IsLocalBranch(Branch branch)
        {
            return !branch.IsRemote;
        }

        bool IsCurrentBranch(Branch branch)
        {
            return StringComparer.Ordinal.Equals(x: headCanonicalName, y: branch.CanonicalName);
        }
    }

    public async ValueTask CommitAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "add -A", cancellationToken: cancellationToken);
            await this.CommitWithMessageAsync(message: message, cancellationToken: cancellationToken);
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    public string GetDefaultBranch(string upstream)
    {
        Repository repo = this.Active;
        Branch headBranch = repo.Branches.FirstOrDefault(IsHeadBranch) ?? throw new GitException($"Failed to find remote branches for {upstream}");
        string target = headBranch.Reference.TargetIdentifier ?? throw new GitException($"Failed to find remote branches for {upstream}");
        string prefix = string.Concat(str0: "refs/remotes/", str1: upstream, str2: "/");

        if (target.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
        {
            return target.Substring(prefix.Length);
        }

        throw new GitException($"Failed to find HEAD branch for remote {upstream}");

        bool IsHeadBranch(Branch branch)
        {
            return branch.IsRemote && StringComparer.Ordinal.Equals(x: branch.RemoteName, y: upstream) && StringComparer.Ordinal.Equals(x: branch.UpstreamBranchCanonicalName, y: "refs/heads/HEAD");
        }
    }

    public bool HasUncommittedChanges()
    {
        return this.Active.RetrieveStatus()
                   .IsDirty;
    }

    public async ValueTask CommitNamedAsync(string message, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        try
        {
            foreach (string file in files)
            {
                await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"add {file}", cancellationToken: cancellationToken);
            }

            await this.CommitWithMessageAsync(message: message, cancellationToken: cancellationToken);
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    public async ValueTask PushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "push", cancellationToken: cancellationToken);
            Console.WriteLine($"Pushed {this.Active.Refs.Head.CanonicalName}!");
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    public async ValueTask PushOriginAsync(string branchName, string upstream, CancellationToken cancellationToken)
    {
        try
        {
            await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"push --set-upstream {upstream} {branchName} -v", cancellationToken: cancellationToken);
            Console.WriteLine($"Pushed {this.Active.Refs.Head.CanonicalName} to {upstream}! ({branchName})");
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    public bool DoesBranchExist(string branchName)
    {
        return this.Active.Branches.Any(Match);

        bool Match(Branch branch)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x: branch.FriendlyName, y: branchName);
        }
    }

    public void CreateBranch(string branchName)
    {
        Repository repo = this.Active;
        Branch? existingBranch = repo.Branches.FirstOrDefault(b => StringComparer.Ordinal.Equals(x: b.FriendlyName, y: branchName));

        if (existingBranch is not null)
        {
            this.Active.Checkout(tree: existingBranch.Tip.Tree, paths: null, options: GitCheckoutOptions);

            return;
        }

        this.Active.Branches.Add(name: branchName, commit: repo.Head.Tip);
    }

    public string HeadRev => this.Active.Head.Tip.Sha;

    public bool HasSubmodules => this.Active.Submodules.Any();

    public async ValueTask RemoveBranchesForPrefixAsync(string branchForUpdate, string branchPrefix, string upstream, CancellationToken cancellationToken)
    {
        Repository repo = this.Active;
        string headCanonicalName = repo.Head.CanonicalName;
        IReadOnlyList<Branch> branchesToRemove = [..repo.Branches.Where(IsMatchingBranch)];

        foreach (Branch branch in branchesToRemove)
        {
            await this.DeleteBranchAsync(branch: branch.FriendlyName, upstream: upstream, cancellationToken: cancellationToken);
        }

        bool IsCurrentBranch(Branch branch)
        {
            return StringComparer.Ordinal.Equals(x: headCanonicalName, y: branch.CanonicalName);
        }

        bool IsMatchingBranch(Branch branch)
        {
            if (IsCurrentBranch(branch))
            {
                return false;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(x: branch.FriendlyName, y: branchForUpdate))
            {
                return false;
            }

            return branch.FriendlyName.StartsWith(value: branchPrefix, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
    }

    public DateTimeOffset GetLastCommitDate()
    {
        return this.Active.Head.Tip.Author.When;
    }

    public IReadOnlyCollection<string> GetRemoteBranches(string upstream)
    {
        const string prefix = "refs/heads/";

        return this.Active.Branches.Where(IsRemoteBranch)
                   .Select(b => b.UpstreamBranchCanonicalName.Substring(prefix.Length))
                   .Where(b => !StringComparer.Ordinal.Equals(x: b, y: "HEAD"))
                   .ToArray();

        bool IsRemoteBranch(Branch branch)
        {
            return branch.IsRemote && StringComparer.Ordinal.Equals(x: branch.RemoteName, y: upstream) &&
                   branch.UpstreamBranchCanonicalName.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal);
        }
    }

    private void ResetRepo()
    {
        this._repo?.Dispose();
        this._repo = null;
    }

    private Remote GetRemote(string upstream)
    {
        return this.Active.Network.Remotes[upstream] ?? throw new GitException($"Could not find upstream origin {upstream}");
    }

    private async ValueTask PruneAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "prune", cancellationToken: cancellationToken);
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    private async Task CleanRepoAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "clean -f -x -d", cancellationToken: cancellationToken);
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    private ValueTask<(string[] Output, int ExitCode)> FetchRemoteAsync(Remote remote, in CancellationToken cancellationToken)
    {
        try
        {
            return GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"fetch --prune --recurse-submodules {remote.Name}", cancellationToken: cancellationToken);
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    private async Task CommitWithMessageAsync(string message, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"commit -m \"{message}\"", cancellationToken: cancellationToken);
    }

    private void UpdateRepoStatus()
    {
        this.ResetRepo();
    }

    private async ValueTask DeleteBranchAsync(string branch, string upstream, CancellationToken cancellationToken)
    {
        try
        {
            await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"branch -D {branch}", cancellationToken: cancellationToken);
            this.UpdateRepoStatus();

            string upstreamBranch = string.Concat(str0: upstream, str1: "/", str2: branch);

            if (this.Active.Branches.Any(b => b.FriendlyName == upstreamBranch))
            {
                await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"push ${upstream} :{branch}", cancellationToken: cancellationToken);
            }
        }
        finally
        {
            this.UpdateRepoStatus();
        }
    }

    private static Repository OpenRepository(string workDir)
    {
        return new(Repository.Discover(workDir));
    }
}