using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Credfeto.Extensions.Linq;

namespace Credfeto.DotNet.Repo.Tools.Models;

public static class RepoContextExtensions
{
    public static bool HasDotNetFiles(this in RepoContext repoContext,
                                      [NotNullWhen(true)] out string? sourceDirectory,
                                      [NotNullWhen(true)] out IReadOnlyList<string>? solutions,
                                      [NotNullWhen(true)] out IReadOnlyList<string>? projects)
    {
        string sourceFolder = BuildSourceFolder(repoContext);

        if (!Directory.Exists(sourceFolder))
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        string[] foundSolutions = Directory.GetFiles(path: sourceFolder, searchPattern: "*.sln", searchOption: SearchOption.AllDirectories);

        if (foundSolutions.Length == 0)
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        string[] foundProjects = Directory.GetFiles(path: sourceFolder, searchPattern: "*.csproj", searchOption: SearchOption.AllDirectories);

        if (foundProjects.Length == 0)
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        sourceDirectory = sourceFolder;
        solutions = foundSolutions;
        projects = foundProjects;

        return true;
    }

    private static string BuildSourceFolder(in RepoContext repoContext)
    {
        return Path.Combine(path1: repoContext.WorkingDirectory, path2: "src");
    }

    public static bool HasSubModules(this in RepoContext repoContext)
    {
        return repoContext.Repository.HasSubmodules;
    }

    public static bool HasDockerFiles(this in RepoContext repoContext)
    {
        return repoContext.HasFiles(searchPattern: "Dockerfile");
    }

    public static bool HasPython(this in RepoContext repoContext)
    {
        return repoContext.HasFiles(searchPattern: "requirements.txt");
    }

    private static bool HasFiles(this in RepoContext repoContext, string searchPattern)
    {
        return repoContext.GetFiles(searchPattern: searchPattern)
                          .Any();
    }

    private static IEnumerable<string> GetFiles(this in RepoContext repoContext, string searchPattern)
    {
        return Directory.EnumerateFiles(path: repoContext.WorkingDirectory, searchPattern: searchPattern, searchOption: SearchOption.AllDirectories);
    }

    public static bool HasNonStandardGithubActions(this in RepoContext repoContext)
    {
        // TODO: Implement this method
        return false;
    }

    private static IEnumerable<string> GetDirectories(this IEnumerable<string> source, int prefix)
    {
        return source.Select(Path.GetDirectoryName)
                     .RemoveNulls()
                     .Select(file => file.Substring(prefix));
    }

    public static bool HasNpmAndYarn(this in RepoContext repoContext, [NotNullWhen(true)] out IReadOnlyList<string>? directories)
    {
        int prefix = repoContext.WorkingDirectory.Length + 1;
        IReadOnlyList<string> dirs =
        [
            ..repoContext.GetFiles("package.json")
                         .GetDirectories(prefix)
        ];

        if (dirs is [])
        {
            directories = null;

            return false;
        }

        directories = dirs;

        return true;
    }
}