using System;

namespace Credfeto.DotNet.Repo.Tools.Packages.Services;

internal static class BranchNaming
{
    public static string BuildInvalidUpdateBranch(string branchPrefix)
    {
        return BuildBranchForVersion(branchPrefix: branchPrefix,
                                     Guid.NewGuid()
                                         .ToString());
    }

    public static string BuildBranchForVersion(string branchPrefix, string version)
    {
        return branchPrefix + version;
    }
}