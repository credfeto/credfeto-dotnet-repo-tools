using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Date.Interfaces;
using Credfeto.Dotnet.Repo.Git.Exceptions;
using LibGit2Sharp;

namespace Credfeto.Dotnet.Repo.Git;

public static class GitUtils
{
    private const string UPSTREAM = "origin";

    private static readonly CheckoutOptions GitCheckoutOptions = new() { CheckoutModifiers = CheckoutModifiers.Force };

    private static readonly CloneOptions GitCloneOptions = new() { Checkout = true, IsBare = false, RecurseSubmodules = true, FetchOptions = { Prune = true, TagFetchMode = TagFetchMode.All } };

    private static readonly CommitOptions GitCommitOptions = new() { AllowEmptyCommit = false, AmendPreviousCommit = false };

    public static string GetFolderForRepo(string repoUrl)
    {
        string work = repoUrl.TrimEnd('/');

        // Extract the folder from the repo name
        string folder = work.Substring(work.LastIndexOf('/') + 1);

        int lastDot = folder.LastIndexOf('.');

        if (lastDot > 0)
        {
            return folder.Substring(startIndex: 0, length: lastDot);
        }

        return folder;
    }

    public static async ValueTask<Repository> OpenOrCloneAsync(string workDir, string repoUrl, CancellationToken cancellationToken)
    {
        string repoDir = Path.Combine(path1: workDir, GetFolderForRepo(repoUrl));

        if (Directory.Exists(repoDir))
        {
            Repository repo = OpenRepository(repoDir);

            await ResetToMasterAsync(repo: repo, upstream: UPSTREAM, cancellationToken: cancellationToken);

            return repo;

            // TODO: Also switch to main & fetch
        }

        return await CloneRepositoryAsync(workDir: workDir, destinationPath: repoDir, repoUrl: repoUrl, cancellationToken: cancellationToken);
    }

    public static async ValueTask ResetToMasterAsync(Repository repo, string upstream, CancellationToken cancellationToken)
    {
        Remote remote = repo.Network.Remotes[upstream] ?? throw new GitException($"Could not find upstream origin {upstream}");

        string defaultBranch = GetDefaultBranch(repo: repo, upstream: upstream);
        await FetchRemoteAsync(repo: repo, remote: remote, cancellationToken: cancellationToken);

        repo.Reset(resetMode: ResetMode.Hard, commit: repo.Head.Tip);

        await CleanRepoAsync(repo: repo, cancellationToken: cancellationToken);

        repo.Checkout(tree: repo.Branches[defaultBranch].Tip.Tree, paths: null, options: GitCheckoutOptions);

        repo.Reset(resetMode: ResetMode.Hard, commit: repo.Head.Tip);

        await CleanRepoAsync(repo: repo, cancellationToken: cancellationToken);

        // # NOTE Loses all local commits on master
        // & git -C $repoPath reset --hard $upstreamBranch 2>&1 | Out-Null
        repo.Reset(resetMode: ResetMode.Hard, commit: repo.Branches[upstream + "/" + defaultBranch].Tip);

        await FetchRemoteAsync(repo: repo, remote: remote, cancellationToken: cancellationToken);

        // & git -C $repoPath prune 2>&1 | Out-Null
        await GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, arguments: "prune", cancellationToken: cancellationToken);

        if (HasUncommittedChanges(repo: repo))
        {
            throw new GitException("Failed to reset to " + defaultBranch + " - uncommitted changes");
        }
    }

    private static async Task CleanRepoAsync(Repository repo, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, arguments: "clean -f -x -d", cancellationToken: cancellationToken);
    }

    private static ValueTask<(string[] Output, int ExitCode)> FetchRemoteAsync(Repository repo, Remote remote, in CancellationToken cancellationToken)
    {
        return GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, $"fetch --prune --recurse-submodules {remote.Name}", cancellationToken: cancellationToken);
    }

    public static void RemoveAllLocalBranches(Repository repo)
    {
        IReadOnlyList<Branch> branchesToRemove = [..repo.Branches.Where(b => IsLocalBranch(b) && !IsCurrentBranch(b))];

        foreach (Branch branch in branchesToRemove)
        {
            repo.Branches.Remove(branch);
        }

        static bool IsLocalBranch(Branch branch)
        {
            return !branch.IsRemote;
        }

        bool IsCurrentBranch(Branch branch)
        {
            return StringComparer.Ordinal.Equals(x: repo.Head.CanonicalName, y: branch.CanonicalName);
        }
    }

    public static IReadOnlyCollection<string> GetRemoteBranches(Repository repo, string upstream = UPSTREAM)
    {
        const string prefix = "refs/heads/";

        return repo.Branches.Where(IsRemoteBranch)
                   .Select(b => b.UpstreamBranchCanonicalName.Substring(prefix.Length))
                   .Where(b => !StringComparer.Ordinal.Equals(x: b, y: "HEAD"))
                   .ToArray();

        bool IsRemoteBranch(Branch branch)
        {
            return branch.IsRemote && StringComparer.Ordinal.Equals(x: branch.RemoteName, y: upstream) &&
                   branch.UpstreamBranchCanonicalName.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal);
        }
    }

    public static string GetDefaultBranch(Repository repo, string upstream = UPSTREAM)
    {
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

    internal static bool IsHttps(string repoUrl)
    {
        return repoUrl.StartsWith(value: "https://", comparisonType: StringComparison.OrdinalIgnoreCase);
    }

    private static async ValueTask<Repository> CloneRepositoryAsync(string workDir, string destinationPath, string repoUrl, CancellationToken cancellationToken)
    {
        string? path = IsHttps(repoUrl)
            ? Repository.Clone(sourceUrl: repoUrl, workdirPath: destinationPath, options: GitCloneOptions)
            : await CloneSshAsync(sourceUrl: repoUrl, workdirPath: workDir, destinationPath: destinationPath, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new GitException($"Failed to clone repo {repoUrl} to {workDir}");
        }

        return OpenRepository(path);
    }

    private static async ValueTask<string?> CloneSshAsync(string sourceUrl, string workdirPath, string destinationPath, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: workdirPath, $"clone --recurse-submodules {sourceUrl} {destinationPath}", cancellationToken: cancellationToken);

        return destinationPath;
    }

    public static bool HasUncommittedChanges(Repository repo)
    {
        return repo.RetrieveStatus()
                   .IsDirty;
    }

    public static void Commit(Repository repo, string message, ICurrentTimeSource currentTimeSource)
    {
        repo.Index.Add("*");
        repo.Index.Write();

        Signature author = new(name: "Example", email: "example@example.com", currentTimeSource.UtcNow());
        Signature committer = author;
        repo.Commit(message: message, author: author, committer: committer, options: GitCommitOptions);
    }

    public static void CommitNamed(Repository repo, string message, ICurrentTimeSource currentTimeSource, params string[] files)
    {
        foreach (string file in files)
        {
            repo.Index.Add(file);
        }

        repo.Index.Write();

        Signature author = new(name: "Example", email: "example@example.com", currentTimeSource.UtcNow());
        Signature committer = author;
        repo.Commit(message: message, author: author, committer: committer, options: GitCommitOptions);
    }

    public static async ValueTask PushAsync(Repository repo, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, arguments: "push", cancellationToken: cancellationToken);
        Console.WriteLine("Pushed!");
    }

    public static async ValueTask PushOriginAsync(Repository repo, string branchName, string upstream, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, $"--set-upstream {upstream} {branchName} -v", cancellationToken: cancellationToken);
        Console.WriteLine("Pushed!");
    }

    public static bool DoesBranchExist(Repository repo, string branchName, string upstream = UPSTREAM)
    {
        return repo.Branches.Any(b => StringComparer.Ordinal.Equals(x: b.FriendlyName, y: branchName));
    }

    public static void CreateBranch(Repository repo, string branchName)
    {
        Branch? existingBranch = repo.Branches.FirstOrDefault(b => StringComparer.Ordinal.Equals(x: b.FriendlyName, y: branchName));

        if (existingBranch is not null)
        {
            repo.Checkout(tree: existingBranch.Tip.Tree, paths: null, options: GitCheckoutOptions);

            return;
        }

        repo.Branches.Add(name: branchName, commit: repo.Head.Tip);
    }

    public static string GetHeadRev(Repository repository)
    {
        return repository.Head.Tip.Sha;
    }

    public static bool HasSubmodules(Repository repo)
    {
        return repo.Submodules.Any();
    }

    public static async ValueTask RemoveBranchesForPrefixAsync(Repository repo, string branchForUpdate, string branchPrefix, string upstream, CancellationToken cancellationToken)
    {
        IReadOnlyList<Branch> branchesToRemove = [..repo.Branches.Where(IsMatchingBranch)];

        foreach (Branch branch in branchesToRemove)
        {
            await DeleteBranchAsync(repo: repo, branch: branch.FriendlyName, upstream: upstream, cancellationToken: cancellationToken);
        }

        bool IsCurrentBranch(Branch branch)
        {
            return StringComparer.Ordinal.Equals(x: repo.Head.CanonicalName, y: branch.CanonicalName);
        }

        bool IsMatchingBranch(Branch branch)
        {
            if (IsCurrentBranch(branch))
            {
                return false;
            }

            if (StringComparer.Ordinal.Equals(x: branch.FriendlyName, y: branchForUpdate))
            {
                return false;
            }

            return branch.FriendlyName.StartsWith(value: branchPrefix, comparisonType: StringComparison.Ordinal);
        }

        /*
          function Git-RemoveBranchesForPrefix {
             param(
                 [string]$repoPath = $(throw "Git-RemoveBranchesForPrefix: repoPath not specified"),
                 [string]$branchForUpdate = $(throw "Git-RemoveBranchesForPrefix: branchForUpdate not specified"),
                 [string]$branchPrefix = $(throw "Git-RemoveBranchesForPrefix: branchPrefix not specified")
                 )

                 Log -message "Git-RemoveBranchesForPrefix: $repoPath ($branchForUpdate, $branchPrefix)"

                 [string]$upstream = "origin"

                 Git-ValidateBranchName -branchName $branchPrefix -method "Git-RemoveBranchesForPrefix"

                 [string[]]$remoteBranches = Git-GetRemoteBranches -repoPath $repoFolder -upstream $upstream

                 Log -message "Looking for branches to remove based on prefix: $branchPrefix"
                 foreach($branch in $remoteBranches) {
                     if($branchForUpdate) {
                         if($branch -eq $branchForUpdate) {
                             Log -message "- Skipping branch just pushed to: $branch"
                             continue
                         }
                     }

                     if($branch.StartsWith($branchPrefix)) {
                         Log -message "+ Deleting older branch for package: $branch"
                         Git-DeleteBranch -branchName $branch -repoPath $repoFolder
                     }
                 }
             }
         */
    }

    private static async ValueTask DeleteBranchAsync(Repository repo, string branch, string upstream, CancellationToken cancellationToken)
    {
        await GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, $"branch -D {branch}", cancellationToken: cancellationToken);

        string upstreamBranch = string.Concat(str0: upstream, str1: "/", str2: branch);

        if (repo.Branches.Any(b => b.FriendlyName == upstreamBranch))
        {
            await GitCommandLine.ExecAsync(repoPath: repo.Info.WorkingDirectory, $"push ${upstream} :{branch}", cancellationToken: cancellationToken);
        }
    }

    public static DateTimeOffset GetLastCommitDate(Repository repo)
    {
        return repo.Head.Tip.Author.When;
    }

    private static Repository OpenRepository(string workDir)
    {
        string found = Repository.Discover(workDir);

        return new(found);
    }
}