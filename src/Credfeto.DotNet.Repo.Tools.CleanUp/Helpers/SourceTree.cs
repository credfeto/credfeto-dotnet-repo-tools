using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Helpers;

public static class SourceTree
{
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ExcludedDirectories = FrozenSet
        .Create(StringComparer.Ordinal, ".git", "bin", "obj")
        .GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        AttributesToSkip = default,
    };

    public static IEnumerable<string> EnumerateFiles(string directory, IReadOnlyList<string> searchPatterns)
    {
        return new FileSystemEnumerable<string>(
            directory: directory,
            transform: static (ref entry) => entry.ToSpecifiedFullPath(),
            options: RecursiveOptions
        )
        {
            ShouldRecursePredicate = static (ref entry) => !ExcludedDirectories.Contains(entry.FileName),
            ShouldIncludePredicate = (ref entry) =>
                !entry.IsDirectory && MatchesAny(searchPatterns: searchPatterns, fileName: entry.FileName),
        };
    }

    private static bool MatchesAny(IReadOnlyList<string> searchPatterns, ReadOnlySpan<char> fileName)
    {
        foreach (string searchPattern in searchPatterns)
        {
            if (FileSystemName.MatchesSimpleExpression(expression: searchPattern, name: fileName, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }
}
