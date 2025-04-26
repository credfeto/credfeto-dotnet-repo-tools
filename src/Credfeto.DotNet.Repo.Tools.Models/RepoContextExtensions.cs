using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Credfeto.Extensions.Linq;

namespace Credfeto.DotNet.Repo.Tools.Models;

public static class RepoContextExtensions
{
    public static bool HasDotNetSolutions(
        this in RepoContext repoContext,
        [NotNullWhen(true)] out string? sourceDirectory,
        [NotNullWhen(true)] out IReadOnlyList<string>? solutions
    )
    {
        string sourceFolder = BuildSourceFolder(repoContext);

        if (!Directory.Exists(sourceFolder))
        {
            sourceDirectory = null;
            solutions = null;

            return false;
        }

        IReadOnlyList<string> foundSolutions = [.. GetFiles(basePath: sourceFolder, searchPattern: "*.sln")];

        if (foundSolutions is [])
        {
            sourceDirectory = null;
            solutions = null;

            return false;
        }

        sourceDirectory = sourceFolder;
        solutions = foundSolutions;

        return true;
    }

    public static bool HasDotNetFiles(
        this in RepoContext repoContext,
        [NotNullWhen(true)] out string? sourceDirectory,
        [NotNullWhen(true)] out IReadOnlyList<string>? solutions,
        [NotNullWhen(true)] out IReadOnlyList<string>? projects
    )
    {
        if (
            !repoContext.HasDotNetSolutions(out string? foundSourceDirectory, out IReadOnlyList<string>? foundSolutions)
        )
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        IReadOnlyList<string> foundProjects = [.. GetFiles(basePath: foundSourceDirectory, searchPattern: "*.csproj")];

        if (foundProjects is [])
        {
            sourceDirectory = null;
            solutions = null;
            projects = null;

            return false;
        }

        sourceDirectory = foundSourceDirectory;
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
        return repoContext.GetFiles(searchPattern: searchPattern).Any();
    }

    public static bool HasNonStandardGithubActions(this in RepoContext repoContext, string templatePath)
    {
        string templateActionsPath = Path.Combine(path1: templatePath, path2: ".github", path3: "actions");
        string repoActionsPath = Path.Combine(path1: repoContext.WorkingDirectory, path2: ".github", path3: "actions");

        string templateWorkflowsPath = Path.Combine(
            path1: repoContext.WorkingDirectory,
            path2: ".github",
            path3: "workflows"
        );
        string repoWorkflowsPath = Path.Combine(
            path1: repoContext.WorkingDirectory,
            path2: ".github",
            path3: "workflows"
        );

        return HasAdditionalFiles(templatePath: templateActionsPath, repoPath: repoActionsPath, searchPattern: "*.yml")
            || HasAdditionalFiles(
                templatePath: templateWorkflowsPath,
                repoPath: repoWorkflowsPath,
                searchPattern: "*.yml"
            );
    }

    private static bool HasAdditionalFiles(string templatePath, string repoPath, string searchPattern)
    {
        if (!Directory.Exists(repoPath))
        {
            return false;
        }

        if (!Directory.Exists(templatePath))
        {
            return true;
        }

        IEnumerable<string> repoFiles = GetFiles(basePath: repoPath, searchPattern: searchPattern)
            .WithoutPrefix(repoPath.Length);
        IEnumerable<string> templateFiles = GetFiles(basePath: templatePath, searchPattern: searchPattern)
            .WithoutPrefix(templatePath.Length);

        return repoFiles.Except(second: templateFiles, comparer: StringComparer.Ordinal).Any();
    }

    private static IEnumerable<string> GetFiles(this in RepoContext repoContext, string searchPattern)
    {
        return GetFiles(basePath: repoContext.WorkingDirectory, searchPattern: searchPattern);
    }

    private static IEnumerable<string> GetFiles(string basePath, string searchPattern)
    {
        return Directory.EnumerateFiles(
            path: basePath,
            searchPattern: searchPattern,
            searchOption: SearchOption.AllDirectories
        );
    }

    private static IEnumerable<string> GetDirectoriesOfFiles(this IEnumerable<string> source)
    {
        return source.Select(Path.GetDirectoryName).RemoveNulls().Distinct(StringComparer.Ordinal);
    }

    private static IEnumerable<string> WithoutPrefix(this IEnumerable<string> source, int prefix)
    {
        return source.Select(file => file[prefix..]);
    }

    public static bool HasNpmAndYarn(
        this in RepoContext repoContext,
        [NotNullWhen(true)] out IReadOnlyList<string>? directories
    )
    {
        IReadOnlyList<string> dirs =
        [
            .. repoContext
                .GetFiles("package.json")
                .GetDirectoriesOfFiles()
                .WithoutPrefix(repoContext.WorkingDirectory.Length),
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
