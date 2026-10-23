using System;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests;

internal static class TextOccurrences
{
    public static int Count(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value: value, comparisonType: StringComparison.Ordinal);

        while (index >= 0)
        {
            ++count;
            index = text.IndexOf(
                value: value,
                startIndex: index + value.Length,
                comparisonType: StringComparison.Ordinal
            );
        }

        return count;
    }
}
