using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Credfeto.Dotnet.Repo.Git.Exceptions;
using LibGit2Sharp;

namespace Credfeto.Dotnet.Repo.Git;

public static class GitUtils
{
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

    public static Repository OpenOrClone(string workDir, string repoUrl)
    {
        string repoDir = Path.Combine(path1: workDir, GetFolderForRepo(repoUrl));

        if (Directory.Exists(repoDir))
        {
            return OpenRepository(repoDir);

            // TODO: Also switch to main & fetch
        }

        return CloneRepository(workDir: repoDir, repoUrl: repoUrl);
    }

    public static IReadOnlyCollection<string> GetRemoteBranches(Repository repo, string upstream = "origin")
    {
        /*
         Log -message "Git-GetRemoteBranches: $repoPath ($upstream)"

           [string]$repoPath = GetRepoPath -repoPath $repoPath

           [string[]]$result = git -C $repoPath branch --remote 2>&1

           $branches = @()

           [string]$remotePrefix = "$upstream/"

           Log -message "Looking for Remote Branches for : $remotePrefix"

           foreach($item in $result) {
               [string]$branch = $item.Trim()
               if(!$branch.StartsWith($remotePrefix)) {
                   Log -message "- Skipping $branch"
                   continue
               }

               $branch = $branch.SubString($remotePrefix.Length)
               $branch = $branch.Split(" ")[0]
               if($branch -eq "HEAD") {
                   Log -message "- Skipping $branch"
                   continue
               }

               Log -message "+ Found $upstream/$branch"
               $branches += $branch
           }

           return [string[]]$branches
         */

        const string prefix = "refs/heads/";

        return repo.Branches.Where(IsRemoteBranch)
                   .Select(b => b.UpstreamBranchCanonicalName.Substring(prefix.Length))
                   .ToArray();

        bool IsRemoteBranch(Branch branch)
        {
            return branch.IsRemote && StringComparer.Ordinal.Equals(x: branch.RemoteName, y: upstream) && branch.UpstreamBranchCanonicalName.StartsWith(prefix, StringComparison.Ordinal);
        }
    }

    public static string GetDefaultBranch(Repository repo, string upstream = "origin")
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

    private static Repository CloneRepository(string workDir, string repoUrl)
    {
        string? path = Repository.Clone(sourceUrl: repoUrl, workdirPath: workDir);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new GitException($"Failed to clone repo {repoUrl} to {workDir}");
        }

        return OpenRepository(path);
    }

    private static Repository OpenRepository(string workDir)
    {
        string found = Repository.Discover(workDir);

        return new(found);
    }
}

/*
$env:GIT_REDIRECT_STDERR="2>&1"

   function IsOnRamDisk {
       param(
           [string]$Path
       )

       if($IsLinux -eq $true) {
           if($Path.StartsWith("/zram/")) {
               return $true
           }
       }

       return $false
   }

   function Git-ValidateBranchName {
   param (
       [string] $branchName = $(throw "Git-ValidateBranchName: branchName not specified"),
       [string] $method = $(throw "Git-ValidateBranchName: method not specified")

   )

       if($branchName -eq $null) {
           throw "$($method) : Invalid branch (null)"
       }

       if($branchName -eq "") {
           throw "$($method) : Invalid branch: [$branchName]"
       }

       if($branchName.Contains("//")) {
           throw "$($method) : Invalid branch: [$branchName]"
       }
   }

   function GetRepoPath {
       param(
           [string] $repoPath
       )

       if([string]::IsNullOrWhiteSpace($repoPath)) {
           $currentDir = Get-Location
           $repoPath = $currentDir.Path
       }

       if([string]::IsNullOrWhiteSpace($repoPath)) {
           throw "Could not determine repo path"
       }

       Log -message "Using Repo: $repoPath"

       return $repoPath
   }


   function Git-GetDefaultBranch {
   param(
       [string] $repoPath = $(throw "Get-GetDefaultBranch: repoPath not specified"),
       [string] $upstream = "origin"
       )

       Log -message "Git-GetDefaultBranch: $repoPath ($upstream)"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string[]]$result = git -C $repoPath remote show $upstream 2>&1

       [string] $branch =  $result | Select-String -Pattern 'HEAD branch: (.*)' -CaseSensitive | %{$_.Matches.Groups[1].value}

       return $branch.Trim()
   }

   function Git-GetRemoteBranches {
   param(
           [string] $repoPath = $(throw "Git-GetRemoteBranches: repoPath not specified"),
           [string] $upstream = "origin"
       )

       Log -message "Git-GetRemoteBranches: $repoPath ($upstream)"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string[]]$result = git -C $repoPath branch --remote 2>&1

       $branches = @()

       [string]$remotePrefix = "$upstream/"

       Log -message "Looking for Remote Branches for : $remotePrefix"

       foreach($item in $result) {
           [string]$branch = $item.Trim()
           if(!$branch.StartsWith($remotePrefix)) {
               Log -message "- Skipping $branch"
               continue
           }

           $branch = $branch.SubString($remotePrefix.Length)
           $branch = $branch.Split(" ")[0]
           if($branch -eq "HEAD") {
               Log -message "- Skipping $branch"
               continue
           }

           Log -message "+ Found $upstream/$branch"
           $branches += $branch
       }

       return [string[]]$branches
   }

   function Git-RemoveAllLocalBranches {
   param(
           [string] $repoPath = $(throw "Git-RemoveAllLocalBranches: repoPath not specified")
       )

       Log -message "Git-RemoveAllLocalBranches: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string[]]$result = git -C $repoPath branch 2>&1
       Log -message "Found: ..."
       Log-Batch -messages $result
       foreach($item in $result) {
           [string]$branch = $item.Trim()
           Log -message "Found: $branch"
           if(!$branch.StartsWith("* ")) {
               [string[]]$complete = git -C $repoPath branch -d $branch 2>&1
               Log -message "Removed: $branch : $complete"
           }
       }
   }

   function Git-ResetToMaster {
   param(
           [string] $repoPath = $(throw "Git-ResetToMaster: repoPath not specified")
       )

       Log -message "Git-ResetToMaster: $repoPath"

       $repack = IsOnRamDisk -Path $repoPath

       [string]$upstream = "origin";
       [string]$repoPath = GetRepoPath -repoPath $repoPath

       $head = Git-Get-HeadRev -repoPath $repoPath

       [string]$defaultBranch = Git-GetDefaultBranch -repoPath $repoPath -upstream $upstream
       [string]$upstreamBranch = "$upstream/$defaultBranch"

       # junk any existing checked out files
       & git -C $repoPath reset HEAD --hard 2>&1 | Out-Null
       & git -C $repoPath clean -f -x -d 2>&1 | Out-Null
       & git -C $repoPath checkout $defaultBranch 2>&1 | Out-Null
       & git -C $repoPath reset HEAD --hard 2>&1 | Out-Null
       & git -C $repoPath clean -f -x -d 2>&1 | Out-Null
       & git -C $repoPath fetch --recurse-submodules 2>&1 | Out-Null

       # NOTE Loses all local commits on master
       & git -C $repoPath reset --hard $upstreamBranch 2>&1 | Out-Null
       & git -C $repoPath remote update $upstream --prune 2>&1 | Out-Null
       & git -C $repoPath prune 2>&1 | Out-Null

       $newHead = Git-Get-HeadRev -repoPath $repoPath
       if($head -ne $newHead) {
           if(!$repack) {
               # ONLY GC if head is different, i.e. something has changed
               & git -C $repoPath gc --aggressive --prune 2>&1 | Out-Null
           }
       }

       Git-RemoveAllLocalBranches -repoPath $repoPath

       return $newHead
   }

   function Git-HasUnCommittedChanges {
   param(
       [string] $repoPath = $(throw "Git-HasUnCommittedChanges: repoPath not specified")
       )

       Log -message "Git-HasUnCommittedChanges: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string[]]$result = git -C $repoPath diff --no-patch --exit-code 2>&1
       if(!$?) {
           Log-Batch -messages $result
           return $true
       }

       Log-Batch -messages $result
       return $false
   }

   function Git-GetFolderForRepo {
   param(
       [string] $repo = $(throw "Git-GetFolderForRepo: repo not specified")
       )

       Log -message "Git-GetFolderForRepo: $repo"

       # Extract the folder from the repo name
       [string] $folder = $repo.Substring($repo.LastIndexOf("/")+1)
       $folder = $folder.SubString(0, $folder.LastIndexOf("."))

       return $folder
   }

   function Git-EnsureSynchronised {
   param(
       [string] $repo = $(throw "Git-EnsureSynchronised: repo not specified"),
       [string] $repoFolder = $(throw "Git-EnsureSynchronised: repoFolder not specified")
       )

       Log -message "Git-EnsureSynchronised: $repoFolder ($repo)"

       Log -message "Repo: $repo"
       Log -message "Folder: $repoFolder"

       [string]$gitHead = Join-Path -Path $repoFolder -ChildPath ".git"
       [string]$gitHead = Join-Path -Path $gitHead -ChildPath "HEAD"

       Log -message "Head: $gitHead"

       [bool]$gitHeadCloned = Test-Path -path $gitHead

       if ($gitHeadCloned -eq $True) {
           Log -message "Already Cloned"
           Set-Location -Path $repoFolder

           Git-ResetToMaster -repoPath $repoFolder
       }
       else
       {
           Log -message "Cloning..."
           [string[]]$result = git clone $repo --recurse-submodules 2>&1
           Log-Batch -messages $result
           Set-Location -Path $repoFolder
       }
   }

   function Git-Commit {
   param(
       [string] $repoPath = $(throw "Git-Commit: repoPath not specified"),
       [string] $message = $(throw "Git-Commit: message not specified")
       )

       Log -message "Git-Commit: $repoPath ($message)"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       & git -C $repoPath add -A 2>&1 | Out-Null
       & git -C $repoPath commit -m"$message" 2>&1 | Out-Null
   }

   function Git-Commit-Named {
   param(
       [string] $repoPath = $(throw "Git-Commit-Named: repoPath not specified"),
       [string] $message = $(throw "Git-Commit-Named: message not specified"),
       [String[]] $files = $(throw "Git-Commit-Named: files not specified")
       )

       Log -message "Git-Commit-Named: $repoPath ($message)"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       foreach($file in $files) {
           [string]$fileUnix = $file.Replace("\", "/")
           Log -message "Staging $fileUnix"
           & git -C $repoPath add $fileUnix 2>&1 | Out-Null
       }

       & git -C $repoPath commit -m"$message" 2>&1 | Out-Null
   }

   function Git-Push {
   param(
       [string] $repoPath = $(throw "Git-Push: repoPath not specified")
       )

       Log -message "Git-Push: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       & git -C $repoPath push | Out-Null
   }


   function Git-PushOrigin {
   param(
       [string] $repoPath = $(throw "Git-PushOrigin: repoPath not specified"),
       [string] $branchName = $(throw "Git-PushOrigin: branchName not specified")
       )

       Log -message "Git-PushOrigin: $repoPath ($branchName)"

       [string]$upstream = "origin";

       Git-ValidateBranchName -branchName $branchName -method "Git-PushOrigin"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       & git -C $repoPath push --set-upstream $upstream $branchName -v 2>&1 | Out-Null
   }


   function Git-DoesBranchExist {
   param(
       [string] $repoPath = $(throw "Git-DoesBranchExist: repoPath not specified"),
       [string] $branchName = $(throw "Git-DoesBranchExist: branchName not specified")
       )

       Log -message "Git-DoesBranchExist: $repoPath ($branchName)"

       [string]$upstream = "origin";
       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string]$defaultBranch = Git-GetDefaultBranch -repoPath $repoPath -upstream $upstream
       [string]$upstreamBranch = "$upstream/$defaultBranch"

       Git-ValidateBranchName -branchName $branchName -method "Git-DoesBranchExist"

       [string[]]$result = git -C $repoPath branch --remote 2>&1

       [string]$regex = $branchName.replace(".", "\.") + "$"

       $result -match $regex
       if($result -eq $null) {
    return $false;
       }

       [string]$result = $result.Trim()
       if($result -eq $branchName) {
           return $true
       }

       [string]$upstreamBranch = "$upstream/$branchName"
       if($result -eq $upstreamBranch) {
           return $true
       }

       return $false
   }

   function Git-CreateBranch {
   param(
       [string] $repoPath = $(throw "Git-CreateBranch: repoPath not specified"),
       [string] $branchName = $(throw "Git-CreateBranch: branchName not specified")
       )

       Log -message "Git-CreateBranch: $repoPath ($branchName)"

       Git-ValidateBranchName -branchName $branchName -method "Git-CreateBranch"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [bool]$branchExists = Git-DoesBranchExist -branchName $branchName -repoPath $repoPath
       if($branchExists -eq $true) {
           Log -message "Failed to create branch $branchName - branch already exists"
           return $false
       }

       [string[]]$result = git -C $repoPath checkout -b $branchName 2>&1
       if(!$?) {
           Log-Batch -messages $result
           Log -message "Failed to create branch $branchName - Create branch failed - Call failed."
           return $false
       }

       Log-Batch -messages $result
       Log -message "Created branch $branchName"

       return $true;
   }

   function Git-DeleteBranch {
   param(
       [string] $repoPath = $(throw "Git-DeleteBranch: repoPath not specified"),
       [string] $branchName = $(throw "Git-DeleteBranch: branchName not specified")
       )

       Log -message "Git-DeleteBranch: $repoPath ($branchName)"

       [string]$upstream = "origin"

       Git-ValidateBranchName -branchName $branchName -method "Git-DeleteBranch"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [bool]$branchExists = Git-DoesBranchExist -branchName $branchName -repoPath $repoPath
       if($branchExists) {
           & git -C $repoPath branch -D $branchName 2>&1 | Out-Null
       }

       [string]$upstreamBranch = "$upstream/$branchName"
       [bool]$branchExists = Git-DoesBranchExist -branchName $upstreamBranch -repoPath $repoPath
       if($branchExists) {
           & git -C $repoPath push $upstream ":$branchName" 2>&1 | Out-Null
       }

       return $true;
   }

   function Git-ReNormalise {
   param(
       [string] $repoPath = $(throw "Git-ReNormalise: repoPath not specified")
       )

       Log -message "Git-ReNormalise: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       & git -C $repoPath add . --renormalize 2>&1 | Out-Null
       [bool]$hasChanged = Git-HasUnCommittedChanges -repoPath $repoPath
       if($hasChanged -eq $true) {
           & git -C $repoPath commit -m"Renormalised files" 2>&1 | Out-Null
           & git -C $repoPath push 2>&1 | Out-Null
       }
   }


   function Git-Get-HeadRev {
   param(
       [string] $repoPath = $(throw "Git-Get-HeadRev: repoPath not specified")
       )

       Log -message "Git-Get-HeadRev: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string[]]$result = git -C $repoPath rev-parse HEAD 2>&1

       Log -message "Head Rev"
       Log-Batch -messages $result

       if(!$?) {
           Log-Batch -messages $result
           Log -message "Failed to get head rev"
           return $null
       }

       [string]$rev = $result.Trim()
       Log -message "Head Rev: $rev"

       return $rev
   }

   function Git-HasSubmodules {
       param(
       [string] $repoPath = $(throw "Git-HasSubmodules: repoPath not specified")
       )

       Log -message "Git-HasSubmodules: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       [string[]]$result = git -C $repoPath submodule 2>&1

       if(!$?) {
           Log-Batch -messages $result
           Log -message "Failed to get submodules."
           return $false
       }

       if($result -eq $null -or $result.Trim() -eq "")  {
           return $false
       }

       Log -message "Submodules found:"
       Log -message $result

       return $true
   }

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

   function Get-GetLastCommitDate {
   param(
       [string] $repoPath = $(throw "Get-GetLastCommitDate: repoPath not specified")
       )

       Log -message "Git-GetLastCommitDate: $repoPath"

       [string]$repoPath = GetRepoPath -repoPath $repoPath

       $unixTime = git -C $repoPath log -1 --format=%ct 2>&1

       [DateTime]$when = [DateTimeOffset]::FromUnixTimeSeconds($unixTime).UtcDateTime

       return $when
   }

   function Git-LoadRepoList {
   param(
       [string] $repoFile = $(throw "Git-LoadRepoList: repoPath not specified")
       )

       Log -message "Git-LoadRepoList: $repoFile"

       [string[]] $content = Get-Content -Path $repoFile

       return $content
   }

   Export-ModuleMember -Function Git-GetDefaultBranch
   Export-ModuleMember -Function Git-RemoveAllLocalBranches
   Export-ModuleMember -Function Git-ResetToMaster
   Export-ModuleMember -Function Git-EnsureSynchronised
   Export-ModuleMember -Function Git-HasUnCommittedChanges
   Export-ModuleMember -Function Git-Commit
   Export-ModuleMember -Function Git-Commit-Named
   Export-ModuleMember -Function Git-CreateBranch
   Export-ModuleMember -Function Git-DeleteBranch
   Export-ModuleMember -Function Git-Push
   Export-ModuleMember -Function Git-PushOrigin
   Export-ModuleMember -Function Git-DoesBranchExist
   Export-ModuleMember -Function Git-LoadRepoList
   Export-ModuleMember -Function Git-GetFolderForRepo
   Export-ModuleMember -Function Git-Get-HeadRev
   Export-ModuleMember -Function Git-ReNormalise
   Export-ModuleMember -Function Git-HasSubmodules
   Export-ModuleMember -Function Git-GetRemoteBranches
   Export-ModuleMember -Function Git-RemoveBranchesForPrefix
   Export-ModuleMember -Function Get-GetLastCommitDate

*/