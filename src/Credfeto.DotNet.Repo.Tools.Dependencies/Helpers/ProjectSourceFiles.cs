using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;

public static class ProjectSourceFiles
{
    private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static IReadOnlyList<string> Find(string projectFolder, string searchPattern)
    {
        return
        [
            .. Directory
                .EnumerateFiles(
                    path: projectFolder,
                    searchPattern: searchPattern,
                    searchOption: SearchOption.AllDirectories
                )
                .Where(file => !IsBuildOutput(projectFolder: projectFolder, file: file)),
        ];
    }

    public static bool IsBuildOutput(string projectFolder, string file)
    {
        string? relativeFolder = Path.GetDirectoryName(Path.GetRelativePath(relativeTo: projectFolder, path: file));

        if (string.IsNullOrEmpty(relativeFolder))
        {
            return false;
        }

        return relativeFolder
            .Split(separator: DirectorySeparators, options: StringSplitOptions.RemoveEmptyEntries)
            .Any(IsBuildOutputFolderName);
    }

    private static bool IsBuildOutputFolderName(string folderName)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(x: folderName, y: "obj")
            || StringComparer.OrdinalIgnoreCase.Equals(x: folderName, y: "bin");
    }
}
