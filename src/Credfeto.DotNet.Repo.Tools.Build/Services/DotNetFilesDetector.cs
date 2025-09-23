using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

public sealed class DotNetFilesDetector : IDotNetFilesDetector
{
    private readonly IProjectFinder _projectFinder;

    public DotNetFilesDetector(IProjectFinder projectFinder)
    {
        this._projectFinder = projectFinder;
    }

    public async ValueTask<DotNetFiles> FindAsync(string baseFolder, CancellationToken cancellationToken)
    {
        string sourceFolder = BuildSourceFolder(baseFolder);

        if (!Directory.Exists(sourceFolder))
        {
            return new(SourceDirectory: baseFolder, [], []);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> foundSolutions = GetFiles(basePath: sourceFolder, "*.sln", "*.slnx");

        if (foundSolutions is [])
        {
            return new(SourceDirectory: sourceFolder, Solutions: foundSolutions, []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> foundProjects = await this._projectFinder.FindProjectsAsync(basePath: sourceFolder, cancellationToken: cancellationToken);

        return new(SourceDirectory: sourceFolder, Solutions: foundSolutions, Projects: foundProjects);
    }

    private static string BuildSourceFolder(string baseFolder)
    {
        return Path.Combine(path1: baseFolder, path2: "src");
    }

    [SuppressMessage(category: "Roslynator.Analyzers", checkId: "RCS1231: Spans should be ref read-only", Justification = "Except when they're in a params parameter")]
    private static IReadOnlyList<string> GetFiles(string basePath, params ReadOnlySpan<string> searchPatterns)
    {
        ImmutableArray<string> result = [];

        foreach (string pattern in searchPatterns)
        {
            IReadOnlyList<string> patternResults = GetFiles(basePath: basePath, searchPattern: pattern);

            if (patternResults is not [])
            {
                result = result.AddRange(patternResults);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> GetFiles(string basePath, string searchPattern)
    {
        return Directory.GetFiles(path: basePath, searchPattern: searchPattern, searchOption: SearchOption.AllDirectories);
    }
}