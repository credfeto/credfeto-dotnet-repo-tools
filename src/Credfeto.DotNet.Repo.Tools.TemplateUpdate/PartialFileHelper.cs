using System;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class PartialFileHelper
{
    public const string DefaultGloballyMaintainedMarker = "<-- Globally Maintained -->";
    public const string DefaultLocallyMaintainedMarker = "<-- Locally Maintained -->";

    public static string BuildContent(string globalContent, string? existingTargetContent, string globallyMaintainedMarker, string locallyMaintainedMarker)
    {
        string localContent = ExtractLocalContent(existingTargetContent: existingTargetContent, locallyMaintainedMarker: locallyMaintainedMarker);

        return globallyMaintainedMarker + "\n" + globalContent.TrimEnd('\r', '\n') + "\n" + locallyMaintainedMarker + "\n" + localContent;
    }

    public static string BuildContent(string globalContent, string? existingTargetContent)
    {
        return BuildContent(globalContent: globalContent,
                            existingTargetContent: existingTargetContent,
                            globallyMaintainedMarker: DefaultGloballyMaintainedMarker,
                            locallyMaintainedMarker: DefaultLocallyMaintainedMarker);
    }

    private static string ExtractLocalContent(string? existingTargetContent, string locallyMaintainedMarker)
    {
        if (existingTargetContent is null)
        {
            return string.Empty;
        }

        int markerIndex = existingTargetContent.IndexOf(value: locallyMaintainedMarker, comparisonType: StringComparison.Ordinal);

        if (markerIndex < 0)
        {
            return string.Empty;
        }

        string afterMarker = existingTargetContent[(markerIndex + locallyMaintainedMarker.Length)..];

        return afterMarker.TrimStart('\r', '\n');
    }
}
