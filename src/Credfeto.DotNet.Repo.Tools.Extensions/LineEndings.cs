using System;

namespace Credfeto.DotNet.Repo.Tools.Extensions;

public static class LineEndings
{
    private const string CARRIAGE_RETURN_LINE_FEED = "\r\n";
    private const string LINE_FEED = "\n";

    public static string MatchOriginal(string original, string updated)
    {
        string? newLine = DetectDominant(original);

        return newLine is null ? updated : Normalise(content: updated, newLine: newLine);
    }

    public static string? DetectDominant(string content)
    {
        int carriageReturnLineFeeds = 0;
        int lineFeeds = 0;

        for (
            int position = content.IndexOf(value: '\n', startIndex: 0);
            position >= 0;
            position = content.IndexOf(value: '\n', startIndex: position + 1)
        )
        {
            if (position > 0 && content[position - 1] == '\r')
            {
                ++carriageReturnLineFeeds;
            }
            else
            {
                ++lineFeeds;
            }
        }

        if (carriageReturnLineFeeds == 0 && lineFeeds == 0)
        {
            return null;
        }

        return carriageReturnLineFeeds > lineFeeds ? CARRIAGE_RETURN_LINE_FEED : LINE_FEED;
    }

    public static string Normalise(string content, string newLine)
    {
        string lineFeedOnly = content.Replace(
            oldValue: CARRIAGE_RETURN_LINE_FEED,
            newValue: LINE_FEED,
            comparisonType: StringComparison.Ordinal
        );

        return StringComparer.Ordinal.Equals(x: newLine, y: LINE_FEED)
            ? lineFeedOnly
            : lineFeedOnly.Replace(oldValue: LINE_FEED, newValue: newLine, comparisonType: StringComparison.Ordinal);
    }
}
