using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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
        // TODO: Implement this method
        return false;
    }

    public static bool HasPython(this in RepoContext repoContext)
    {
        // TODO: Implement this method
        return false;
    }

    public static bool HasNonStandardGithubActions(this in RepoContext repoContext)
    {
        // TODO: Implement this method
        return false;
    }

    public static bool HasNpmAndYarn(this in RepoContext repoContext, [NotNullWhen(true)]out IReadOnlyList<string>? directories)
    {

    }
}