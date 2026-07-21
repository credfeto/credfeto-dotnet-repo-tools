using System.Collections.Generic;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using FunFair.BuildCheck.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Services;

public sealed class ProjectClassifierTests : TestBase
{
    private static readonly IReadOnlyList<SolutionProject> NoProjects = [];

    private readonly IProjectClassifier _projectClassifier = new ProjectClassifier();

    [Theory]
    [InlineData("FunFair.CodeAnalysis", true)]
    [InlineData("Other", false)]
    public void IsCodeAnalysisSolution(string displayName, bool expected)
    {
        IReadOnlyList<SolutionProject> projects = [new(FileName: $"{displayName}.csproj", DisplayName: displayName)];

        Assert.Equal(expected: expected, actual: this._projectClassifier.IsCodeAnalysisSolution(projects));
    }

    [Fact]
    public void IsCodeAnalysisSolutionWhenNoProjects()
    {
        Assert.False(
            condition: this._projectClassifier.IsCodeAnalysisSolution(NoProjects),
            userMessage: "Should not match when there are no projects"
        );
    }

    [Theory]
    [InlineData("FunFair.Test.Common", true)]
    [InlineData("Other", false)]
    public void IsUnitTestBase(string displayName, bool expected)
    {
        IReadOnlyList<SolutionProject> projects = [new(FileName: $"{displayName}.csproj", DisplayName: displayName)];

        Assert.Equal(expected: expected, actual: this._projectClassifier.IsUnitTestBase(projects));
    }

    [Fact]
    public void IsUnitTestBaseWhenNoProjects()
    {
        Assert.False(
            condition: this._projectClassifier.IsUnitTestBase(NoProjects),
            userMessage: "Should not match when there are no projects"
        );
    }

    [Theory]
    [InlineData("Credfeto.Enumeration.Source.Generation", true)]
    [InlineData("Other", false)]
    public void MustHaveEnumSourceGeneratorAnalyzerPackage(string displayName, bool expected)
    {
        IReadOnlyList<SolutionProject> projects = [new(FileName: $"{displayName}.csproj", DisplayName: displayName)];

        Assert.Equal(
            expected: expected,
            actual: this._projectClassifier.MustHaveEnumSourceGeneratorAnalyzerPackage(projects)
        );
    }

    [Fact]
    public void MustHaveEnumSourceGeneratorAnalyzerPackageWhenNoProjects()
    {
        Assert.False(
            condition: this._projectClassifier.MustHaveEnumSourceGeneratorAnalyzerPackage(NoProjects),
            userMessage: "Should not match when there are no projects"
        );
    }
}
