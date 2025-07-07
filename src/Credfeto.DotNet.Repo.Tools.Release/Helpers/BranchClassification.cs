using System;

namespace Credfeto.DotNet.Repo.Tools.Release.Helpers;

internal static partial class BranchClassification
{
    private const string PACKAGE_UPDATER_BRANCH_PREFIX = "depends/";
    private const string DEPENDABOT_BRANCH_PREFIX = "dependabot/";

    private static bool IsDotNetSdkPreviewUpdate(string branch)
    {
        return branch.StartsWith(value: "depends/sdk/dotnet/", comparisonType: StringComparison.Ordinal) && branch.EndsWith(value: "/preview", comparisonType: StringComparison.Ordinal);
    }
}