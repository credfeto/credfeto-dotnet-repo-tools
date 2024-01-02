using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Git.Helpers;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces.Exceptions;
using Credfeto.DotNet.Repo.Tools.Git.Services.LoggingExtensions;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.Git.Services;

[DebuggerDisplay("{ClonePath}: {WorkingDirectory}")]
internal sealed class GitRepository : IGitRepository
{
    private readonly ILogger _logger;

    private Repository? _repo;

    internal GitRepository(string clonePath, string workingDirectory, Repository? repo, ILogger logger)
    {
        this.ClonePath = clonePath;
        this.WorkingDirectory = workingDirectory;
        this._repo = repo;
        this._logger = logger;
    }

    public Repository Active => this._repo ?? OpenRepository(workDir: this.WorkingDirectory);

    public string ClonePath { get; }

    public string WorkingDirectory { get; }

    public string HeadRev => this.Active.Head.Tip.Sha;

    public bool HasSubmodules => this.Active.Submodules.Any();

    public void Dispose()
    {
        this.ResetActiveRepoLink();
    }

    public async ValueTask ResetToMasterAsync(string upstream, CancellationToken cancellationToken)
    {
        string defaultBranch = this.GetDefaultBranch(upstream: upstream);

        await this.FetchRemoteAsync(upstream: upstream, cancellationToken: cancellationToken);

        this.Active.Reset(resetMode: ResetMode.Hard, commit: this.Active.Head.Tip);

        await this.CleanRepoAsync(cancellationToken: cancellationToken);

        await this.CheckoutAsync(branchName: defaultBranch, cancellationToken: cancellationToken);

        this.Active.Reset(resetMode: ResetMode.Hard, commit: this.Active.Head.Tip);

        await this.CleanRepoAsync(cancellationToken: cancellationToken);

        // # NOTE Loses all local commits on master
        // & git -C $repoPath reset --hard $upstreamBranch 2>&1 | Out-Null
        this.Active.Reset(resetMode: ResetMode.Hard, commit: this.Active.Branches[upstream + "/" + defaultBranch].Tip);

        await this.FetchRemoteAsync(upstream: upstream, cancellationToken: cancellationToken);

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
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "add -A", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Add (all) exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }

            await this.CommitWithMessageAsync(message: message, cancellationToken: cancellationToken);
        }
        finally
        {
            this.ResetActiveRepoLink();
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
                (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"add {file}", cancellationToken: cancellationToken);

                if (exitCode != 0)
                {
                    this._logger.LogWarning($"Add {file} exit code: {exitCode}");

                    foreach (string line in result)
                    {
                        this._logger.LogWarning(line);
                    }
                }
            }

            await this.CommitWithMessageAsync(message: message, cancellationToken: cancellationToken);
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    public async ValueTask PushAsync(CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "push", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Push exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }

            this._logger.LogPushedBranch(this.Active.Refs.Head.CanonicalName);
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    public async ValueTask PushOriginAsync(string branchName, string upstream, CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"push --set-upstream {upstream} {branchName} -v", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Push upstream exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }

            this._logger.LogPushedBranchUpstream(canonicalName: this.Active.Refs.Head.CanonicalName, upstream: upstream, branchName: branchName);
        }
        finally
        {
            this.ResetActiveRepoLink();
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

    public async ValueTask CreateBranchAsync(string branchName, CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"checkout -b {branchName}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Create Branch exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

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

    public async ValueTask CheckoutAsync(string branchName, CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"checkout {branchName}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Checkout branch exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private ValueTask FetchRemoteAsync(string upstream, in CancellationToken cancellationToken)
    {
        Remote remote = this.GetRemote(upstream);

        return this.FetchRemoteAsync(remote: remote, cancellationToken: cancellationToken);
    }

    private void ResetActiveRepoLink()
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
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "prune", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Prune exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private async ValueTask CleanRepoAsync(CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "clean -f -x -d", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Clean exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private async ValueTask FetchRemoteAsync(Remote remote, CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) =
                await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"fetch --prune --recurse-submodules {remote.Name}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Fetch exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private async ValueTask CommitWithMessageAsync(string message, CancellationToken cancellationToken)
    {
        (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"commit -m \"{message}\"", cancellationToken: cancellationToken);

        if (exitCode != 0)
        {
            this._logger.LogWarning($"Commit exit code: {exitCode}");

            foreach (string line in result)
            {
                this._logger.LogWarning(line);
            }
        }
    }

    private async ValueTask DeleteBranchAsync(string branch, string upstream, CancellationToken cancellationToken)
    {
        try
        {
            await this.DeleteLocalBranchAsync(branch: branch, cancellationToken: cancellationToken);

            await this.FetchRemoteAsync(this.GetRemote(upstream), cancellationToken: cancellationToken);

            string upstreamBranch = string.Concat(str0: upstream, str1: "/", str2: branch);

            if (this.Active.Branches.Any(IsRemoteBranch))
            {
                await this.DeleteRemoteBranchAsync(branch: branch, upstream: upstream, cancellationToken: cancellationToken);
            }
            else
            {
                this._logger.LogSkippingDeleteOfUpstreamBranch(branch: branch, upstream: upstream);
            }

            bool IsRemoteBranch(Branch candidateBranch)
            {
                if (candidateBranch.IsRemote && StringComparer.Ordinal.Equals(x: candidateBranch.RemoteName, y: upstream) && StringComparer.Ordinal.Equals(x: candidateBranch.FriendlyName, y: branch))

                {
                    return true;
                }

                if (StringComparer.Ordinal.Equals(x: candidateBranch.FriendlyName, y: upstreamBranch))
                {
                    return true;
                }

                return false;
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private async Task DeleteRemoteBranchAsync(string branch, string upstream, CancellationToken cancellationToken)
    {
        try
        {
            this._logger.LogDeletingUpstreamBranch(branch: branch, upstream: upstream);
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"push ${upstream} :{branch}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Delete remote branch exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private async Task DeleteLocalBranchAsync(string branch, CancellationToken cancellationToken)
    {
        try
        {
            this._logger.LogDeletingLocalBranch(branch);
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"branch -D {branch}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this._logger.LogWarning($"Delete local branch exit code: {exitCode}");

                foreach (string line in result)
                {
                    this._logger.LogWarning(line);
                }
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private static Repository OpenRepository(string workDir)
    {
        return new(Repository.Discover(workDir));
    }
}