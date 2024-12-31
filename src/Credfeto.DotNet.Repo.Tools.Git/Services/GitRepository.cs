using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
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
        this.EnsureNotLocked();

        string defaultBranch = this.GetDefaultBranch(upstream: upstream);

        await this.FetchRemoteAsync(upstream: upstream, cancellationToken: cancellationToken);

        await this.ResetHeadHardAsync(cancellationToken);

        await this.CleanRepoAsync(cancellationToken: cancellationToken);

        await this.CheckoutAsync(branchName: defaultBranch, cancellationToken: cancellationToken);

        await this.ResetHeadHardAsync(cancellationToken);

        await this.CleanRepoAsync(cancellationToken: cancellationToken);

        // # NOTE Loses all local commits on master
        // & git -C $repoPath reset --hard $upstreamBranch 2>&1 | Out-Null
        await this.ResetUpstreamHardAsync(upstream: upstream, branch: defaultBranch, cancellationToken: cancellationToken);

        await this.FetchRemoteAsync(upstream: upstream, cancellationToken: cancellationToken);

        // & git -C $repoPath prune 2>&1 | Out-Null
        await this.PruneAsync(cancellationToken: cancellationToken);

        this.ResetActiveRepoLink();

        if (this.HasUncommittedChanges())
        {
            throw new GitException("Failed to reset to " + defaultBranch + " - uncommitted changes");
        }
    }

    public void RemoveAllLocalBranches()
    {
        this.EnsureNotLocked();

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
        this.EnsureNotLocked();

        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "add -A", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Add (all)");
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
        this.EnsureNotLocked();

        Repository repo = this.Active;
        Branch headBranch = repo.Branches.FirstOrDefault(IsHeadBranch) ?? throw new GitException($"Failed to find remote branches for {upstream}");
        string target = headBranch.Reference.TargetIdentifier ?? throw new GitException($"Failed to find remote branches for {upstream}");
        string prefix = string.Concat(str0: "refs/remotes/", str1: upstream, str2: "/");

        if (target.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
        {
            string branch = target[prefix.Length..];
            this._logger.DefaultBranchForUpstream(branch: branch, upstream: upstream);

            return branch;
        }

        throw new GitException($"Failed to find HEAD branch for remote {upstream}");

        bool IsHeadBranch(Branch branch)
        {
            return IsRemote(branch: branch, upstream: upstream) && StringComparer.Ordinal.Equals(x: branch.UpstreamBranchCanonicalName, y: "refs/heads/HEAD");
        }
    }

    public bool HasUncommittedChanges()
    {
        this.EnsureNotLocked();

        return this.Active.RetrieveStatus()
                   .IsDirty;
    }

    public async ValueTask CommitNamedAsync(string message, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        this.EnsureNotLocked();

        try
        {
            foreach (string file in files)
            {
                (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"add {file}", cancellationToken: cancellationToken);

                if (exitCode != 0)
                {
                    this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Add {file}");
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
        this.EnsureNotLocked();

        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "push", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Push");
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
        this.EnsureNotLocked();

        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"push --set-upstream {upstream} {branchName} -v", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Push");
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
        this.EnsureNotLocked();

        return this.Active.Branches.Any(Match);

        bool Match(Branch branch)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x: branch.FriendlyName, y: branchName);
        }
    }

    public async ValueTask CreateBranchAsync(string branchName, CancellationToken cancellationToken)
    {
        this.EnsureNotLocked();

        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"checkout -b {branchName}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Create Branch {branchName}");
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    [SuppressMessage(category: "Meziantou.Analyzer", checkId: "MA0051: Method is too long", Justification = "Debug logging")]
    public async ValueTask RemoveBranchesForPrefixAsync(string branchForUpdate, string branchPrefix, string upstream, CancellationToken cancellationToken)
    {
        this.EnsureNotLocked();

        Repository repo = this.Active;
        string headCanonicalName = repo.Head.CanonicalName;
        string upstreamBranchForUpdate = BuildUpstreamBranch(upstream: upstream, branch: branchForUpdate);
        string upstreamBranchPrefix = BuildUpstreamBranch(upstream: upstream, branch: branchPrefix);

        IReadOnlyList<Branch> branchesToRemove = [..repo.Branches.Where(IsMatchingBranch)];

        foreach (Branch branch in branchesToRemove)
        {
            string branchName = NormaliseBranchName(branch: branch, upstream: upstream);

            await this.DeleteBranchAsync(branch: branchName, upstream: upstream, cancellationToken: cancellationToken);
        }

        bool IsCurrentBranch(Branch branch)
        {
            return StringComparer.Ordinal.Equals(x: headCanonicalName, y: branch.CanonicalName);
        }

        bool IsMatchingBranch(Branch branch)
        {
            if (IsCurrentBranch(branch))
            {
                // this._logger.LogWarning(
                //     $"* [RemoveBranchesForPrefix] Matched (skip) Current Branch exact {branch.FriendlyName} (branchPrefix: [{upstream}/]{branchPrefix}, branchForUpdate: [{upstream}/]{branchForUpdate})");

                return false;
            }

            if (IsCurrentBranchByName(branch))
            {
                // this._logger.LogWarning(
                //     $"* [RemoveBranchesForPrefix] Matched (Skip) Current Branch for update exact {branch.FriendlyName} (branchPrefix: [{upstream}/]{branchPrefix}, branchForUpdate: [{upstream}/]{branchForUpdate})");

                return false;
            }

            if (IsAlternateMatchBranchByName(branch))
            {
                // this._logger.LogWarning(
                //     $"* [RemoveBranchesForPrefix] Matched for update prefix {branch.FriendlyName} (branchPrefix: [{upstream}/]{branchPrefix}, branchForUpdate: [{upstream}/]{branchForUpdate})");

                return true;
            }

            // this._logger.LogWarning($"* [RemoveBranchesForPrefix] No Match {branch.FriendlyName} (branchPrefix: [{upstream}/]{branchPrefix}, branchForUpdate: [{upstream}/]{branchForUpdate})");

            return false;
        }

        bool IsCurrentBranchByName(Branch branch)
        {
            if (IsExactMatchBranchName(branch: branch, branchName: branchForUpdate))
            {
                return true;
            }

            return IsRemote(branch: branch, upstream: upstream) && IsExactMatchBranchName(branch: branch, branchName: upstreamBranchForUpdate) &&
                   StringComparer.Ordinal.Equals(x: branch.RemoteName, y: upstream);
        }

        bool IsAlternateMatchBranchByName(Branch branch)
        {
            if (IsAlternateMatchBranchByPrefix(branch: branch, branchPrefix: branchPrefix))
            {
                return true;
            }

            return IsRemote(branch: branch, upstream: upstream) && IsAlternateMatchBranchByPrefix(branch: branch, branchPrefix: upstreamBranchPrefix);
        }
    }

    public DateTimeOffset GetLastCommitDate()
    {
        this.EnsureNotLocked();

        return this.Active.Head.Tip.Author.When;
    }

    public IReadOnlyCollection<string> GetRemoteBranches(string upstream)
    {
        this.EnsureNotLocked();

        const string prefix = "refs/heads/";

        return
        [
            ..this.Active.Branches.Where(IsRemoteBranch)
                  .Select(b => b.UpstreamBranchCanonicalName[prefix.Length..])
                  .Where(b => !StringComparer.Ordinal.Equals(x: b, y: "HEAD"))
        ];

        bool IsRemoteBranch(Branch branch)
        {
            return IsRemote(branch: branch, upstream: upstream) && branch.UpstreamBranchCanonicalName.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal);
        }
    }

    private void EnsureNotLocked()
    {
        string lockFile = Path.Combine(path1: this.WorkingDirectory, path2: ".git", path3: "lock.json");

        if (File.Exists(lockFile))
        {
            throw new GitRepositoryLockedException($"Repository {this.ClonePath} at {this.WorkingDirectory} is locked.");
        }
    }

    private async Task ResetUpstreamHardAsync(string upstream, string branch, CancellationToken cancellationToken)
    {
        string branchName = BuildUpstreamBranch(upstream: upstream, branch: branch);

        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"reset {branchName} --hard", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Reset {branchName} --hard");
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private async Task ResetHeadHardAsync(CancellationToken cancellationToken)
    {
        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, arguments: "reset HEAD --hard", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Reset HEAD --hard");
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private static bool IsExactMatchBranchName(Branch branch, string branchName)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(x: branch.FriendlyName, y: branchName);
    }

    private static bool IsAlternateMatchBranchByPrefix(Branch branch, string branchPrefix)
    {
        return branch.FriendlyName.StartsWith(value: branchPrefix, comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemote(Branch branch, string upstream)
    {
        return branch.IsRemote && StringComparer.Ordinal.Equals(x: branch.RemoteName, y: upstream);
    }

    private static string NormaliseBranchName(Branch branch, string upstream)
    {
        return IsRemote(branch: branch, upstream: upstream) && branch.FriendlyName.StartsWith(upstream + "/", comparisonType: StringComparison.Ordinal)
            ? branch.FriendlyName[(upstream.Length + 1)..]
            : branch.FriendlyName;
    }

    private static string BuildUpstreamBranch(string upstream, string branch)
    {
        return string.Concat(str0: upstream, str1: "/", str2: branch);
    }

    public async ValueTask CheckoutAsync(string branchName, CancellationToken cancellationToken)
    {
        this.EnsureNotLocked();

        try
        {
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"checkout {branchName}", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Checkout {branchName}");
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
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Prune");
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
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Clean");
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
                this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Fetch {remote.Name}");
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
            this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Commit \"{message}\"");
        }
    }

    private async ValueTask DeleteBranchAsync(string branch, string upstream, CancellationToken cancellationToken)
    {
        try
        {
            if (this.DoesBranchExist(branch))
            {
                await this.DeleteLocalBranchAsync(branch: branch, cancellationToken: cancellationToken);
            }

            await this.FetchRemoteAsync(this.GetRemote(upstream), cancellationToken: cancellationToken);

            string upstreamBranch = BuildUpstreamBranch(upstream: upstream, branch: branch);

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
                if (IsRemote(branch: candidateBranch, upstream: upstream) && StringComparer.Ordinal.Equals(x: candidateBranch.FriendlyName, y: branch))
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
            (string[] result, int exitCode) = await GitCommandLine.ExecAsync(repoPath: this.WorkingDirectory, $"push {upstream} \":{branch}\"", cancellationToken: cancellationToken);

            if (exitCode != 0)
            {
                this.DumpExitCodeResult(result: result, exitCode: exitCode, prefix: "Delete remote branch");

                // TODO: specific exception for branch deletion
                throw new GitException($"Could not delete remote branch {branch}");
            }
        }
        finally
        {
            this.ResetActiveRepoLink();
        }
    }

    private void DumpExitCodeResult(IReadOnlyList<string> result, int exitCode, string prefix)
    {
        this._logger.LogGitExitCode(prefix: prefix, exitCode: exitCode);

        foreach (string line in result)
        {
            this._logger.LogGitMessage(line);
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
                this.DumpExitCodeResult(result: result, exitCode: exitCode, $"Delete local branch {branch}");

                // TODO: specific exception for branch deletion
                throw new GitException($"Could not delete local branch {branch}");
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