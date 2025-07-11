#if NET9_0_OR_GREATER
#else
using System;

namespace Credfeto.DotNet.Repo.Tools.Release.Helpers;

internal static partial class BranchClassification
{
    public static bool IsDependencyBranch(string branch)
    {
        return IsDependabotBranch(branch) || IsPackageUpdaterBranch(branch);
    }

    private static bool IsPackageUpdaterBranch(string branch)
    {
        return branch.StartsWith(value: PACKAGE_UPDATER_BRANCH_PREFIX, comparisonType: StringComparison.Ordinal)
            && !IsDotNetSdkPreviewUpdate(branch);
    }

    private static bool IsDependabotBranch(string branch)
    {
        return branch.StartsWith(value: DEPENDABOT_BRANCH_PREFIX, comparisonType: StringComparison.Ordinal);
    }
}
#endif
