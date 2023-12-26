using System;
using System.Collections.Generic;
using System.Linq;
using FunFair.BuildCheck.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

internal sealed class ProjectClassifier : IProjectClassifier
{
    public bool IsCodeAnalysisSolution(IReadOnlyList<SolutionProject> projects)
    {
        return HasNamedProject(projects: projects, projectName: "FunFair.CodeAnalysis");
    }

    public bool IsUnitTestBase(IReadOnlyList<SolutionProject> projects)
    {
        return HasNamedProject(projects: projects, projectName: "FunFair.Test.Common");
    }

    public bool MustHaveEnumSourceGeneratorAnalyzerPackage(IReadOnlyList<SolutionProject> projects)
    {
        return HasNamedProject(projects: projects, projectName: "Credfeto.Enumeration.Source.Generation");
    }

    private static bool HasNamedProject(IReadOnlyList<SolutionProject> projects, string projectName)
    {
        return projects.Any(project => StringComparer.InvariantCultureIgnoreCase.Equals((string?)project.DisplayName, y: projectName));
    }
}