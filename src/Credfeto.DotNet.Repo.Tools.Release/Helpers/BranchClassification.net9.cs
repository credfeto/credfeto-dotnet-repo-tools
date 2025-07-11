#if NET9_0_OR_GREATER
using System;
using System.Buffers;

namespace Credfeto.DotNet.Repo.Tools.Release.Helpers;

internal static partial class BranchClassification
{
    private static readonly SearchValues<string> DependencyBranches = SearchValues.Create(
        [PACKAGE_UPDATER_BRANCH_PREFIX, DEPENDABOT_BRANCH_PREFIX],
        comparisonType: StringComparison.OrdinalIgnoreCase
    );

    public static bool IsDependencyBranch(string branch)
    {
        return branch.AsSpan().IndexOfAny(DependencyBranches) == 0 && !IsDotNetSdkPreviewUpdate(branch);
    }
}
#endif
