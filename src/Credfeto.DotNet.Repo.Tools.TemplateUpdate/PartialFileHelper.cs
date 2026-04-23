using System;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class PartialFileHelper
{
    public const string DefaultGloballyMaintainedMarker = "<!-- Globally Maintained -->";
    public const string DefaultLocallyMaintainedMarker = "<!-- Locally Maintained -->";

    public static string BuildContent(string globalContent, string? existingTargetContent, string globallyMaintainedMarker, string locallyMaintainedMarker)
    {
        string extractedGlobalContent = ExtractGlobalContent(content: globalContent, globallyMaintainedMarker: globallyMaintainedMarker, locallyMaintainedMarker: locallyMaintainedMarker);
        string localContent = ExtractLocalContent(existingTargetContent: existingTargetContent, locallyMaintainedMarker: locallyMaintainedMarker);

        return globallyMaintainedMarker + Environment.NewLine + extractedGlobalContent + Environment.NewLine + locallyMaintainedMarker + Environment.NewLine + localContent;
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
        if (string.IsNullOrWhiteSpace(existingTargetContent))
        {
            return string.Empty;
        }

        int markerIndex = existingTargetContent.IndexOf(value: locallyMaintainedMarker, comparisonType: StringComparison.Ordinal);

        if (markerIndex < 0)
        {
            return string.Empty;
        }

        string afterMarker = existingTargetContent[(markerIndex + locallyMaintainedMarker.Length)..];
        string localContent = afterMarker.TrimStart('\r', '\n');

        while (localContent.StartsWith(value: locallyMaintainedMarker, comparisonType: StringComparison.Ordinal))
        {
            localContent = localContent[locallyMaintainedMarker.Length..].TrimStart('\r', '\n');
        }

        return localContent;
    }

    private static string ExtractGlobalContent(string content, string globallyMaintainedMarker, string locallyMaintainedMarker)
    {
        int globalMarkerPos = content.IndexOf(value: globallyMaintainedMarker, comparisonType: StringComparison.Ordinal);

        if (globalMarkerPos >= 0)
        {
            int globalContentStartPos = globalMarkerPos + globallyMaintainedMarker.Length;
            int localMarkerPos = content.IndexOf(value: locallyMaintainedMarker, startIndex: globalContentStartPos, comparisonType: StringComparison.Ordinal);

            if (localMarkerPos >= 0)
            {
                string betweenMarkers = content[globalContentStartPos..localMarkerPos];

                return betweenMarkers.Trim('\r', '\n');
            }

            return content[globalContentStartPos..].Trim('\r', '\n');
        }

        int localOnlyMarkerPos = content.IndexOf(value: locallyMaintainedMarker, comparisonType: StringComparison.Ordinal);

        if (localOnlyMarkerPos < 0)
        {
            return content.TrimEnd('\r', '\n');
        }

        return content[..localOnlyMarkerPos].Trim('\r', '\n');
    }
}
