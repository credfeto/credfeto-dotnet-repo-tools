using System;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class PartialFileHelper
{
    public const string GloballyMaintainedMarker = "<-- Globally Maintained -->";
    public const string LocallyMaintainedMarker = "<-- Locally Maintained -->";

    public static string BuildContent(string globalContent, string? existingTargetContent)
    {
        string localContent = ExtractLocalContent(existingTargetContent);

        return GloballyMaintainedMarker + "\n" + globalContent.TrimEnd('\r', '\n') + "\n" + LocallyMaintainedMarker + "\n" + localContent;
    }

    private static string ExtractLocalContent(string? existingTargetContent)
    {
        if (existingTargetContent is null)
        {
            return string.Empty;
        }

        int markerIndex = existingTargetContent.IndexOf(value: LocallyMaintainedMarker, comparisonType: StringComparison.Ordinal);

        if (markerIndex < 0)
        {
            return string.Empty;
        }

        string afterMarker = existingTargetContent[(markerIndex + LocallyMaintainedMarker.Length)..];

        return afterMarker.TrimStart('\r', '\n');
    }
}
