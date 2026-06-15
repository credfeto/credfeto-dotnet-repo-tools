using System;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class LabelsBuilderTests : LoggingTestBase
{
    private readonly ILabelsBuilder _labelsBuilder;

    public LabelsBuilderTests(ITestOutputHelper output)
        : base(output)
    {
        this._labelsBuilder = new LabelsBuilder();
    }

    [Fact]
    public void BuildLabelsYmlNoProjects()
    {
        (string actual, string _) = this._labelsBuilder.BuildLabelsConfig([]);

        this.Output.WriteLine(actual);

        string expected =
            "---\n"
            + new LabelsYamlBuilder()
                .Add(".NET update", "a870c9", "Update to .net global.json")
                .Add("AI-Work", "ffa500", "Work for an AI Agent")
                .Add("auto-pr", "0000aa", "Pull request created automatically")
                .Add("Blocked", "ff0000", "Blocked by a dependency or external factor")
                .Add("Bug", "d73a4a", "Generic bug fix")
                .Add("C#", "db6baa", "C# Source Files")
                .Add("C# Project", "db6baa", "C# Project Files")
                .Add("C# Solution", "db6baa", "C# Solutions")
                .Add("Change Log", "53fcd4", "Changelog tracking file")
                .Add("Changelog Not Required", "08f5f8", "No changelog entry required for this pull request")
                .Add("Config Change", "d8bb50", "Configuration files changes")
                .Add("dependencies", "0366d6", "Updates to dependencies")
                .Add("DO NOT MERGE", "ff0000", "This pull request should not be merged yet")
                .Add("dotnet", "db6baa", "Dotnet package updates")
                .Add("Editorconfig", "00dead", "Editor config file change")
                .Add("Enhancement", "a2eeef", "Enhancement to project")
                .Add("github-actions", "e09cf4", "Github actions workflow files")
                .Add("High", "ffa500", "High Priority")
                .Add("Low", "cc8899", "Low Priority")
                .Add("Markdown", "5319e7", "Markdown files")
                .Add("Medium", "ffff00", "Medium Priority")
                .Add("Migration Script", "b680e5", "SQL Migration scripts")
                .Add("never-close", "1d76db", "This issue should never be closed — it is a permanent tracking issue")
                .Add("no-pr-activity", "ffff00", "Pull Request has had no activity for a long time")
                .Add("npm", "e99695", "npm package update")
                .Add("On Hold", "ff0000", "Do not work on this")
                .Add("Performance", "0075ca", "Performance enhancement or issue")
                .Add("Powershell", "23bc12", "Powershell Source Files")
                .Add("Read Me", "5319e7", "Repository readme file")
                .Add("Refactor", "30027a", "Code refactoring")
                .Add("Security", "ee0701", "Security issue, e.g. use of insecure packages, or security fix")
                .Add("Setup", "5319e7", "Setup instructions")
                .Add("Solidity", "413cd1", "Solidity Source Files")
                .Add("SQL", "413cd1", "SQL Source Files")
                .Add("Static Code Analysis Rules", "00dead", "Ruleset for static code analysis files")
                .Add("Tech Debt", "30027a", "Technical debt")
                .Add("Unit Tests", "0e8a16", "Unit test and integration test projects")
                .Add("Urgent", "ff0000", "Urgent Priority")
                .Build();

        Assert.Equal(expected: expected, actual: actual);
    }

    [Fact]
    public void BuildLabelerYmlNoProjects()
    {
        (string _, string actual) = this._labelsBuilder.BuildLabelsConfig([]);

        this.Output.WriteLine(actual);

        string expected =
            "---\n"
            + new LabelerYamlBuilder()
                .Add(".NET update", "src/global.json")
                .Add("C#", "./**/*.cs", "./**/*.csproj")
                .Add("C# Project", "./**/*.csproj")
                .Add("C# Solution", "./**/*.sln", "./**/*.slnx")
                .Add("Change Log", "CHANGELOG.md")
                .Add("Config Change", "src/**/*.json", "!src/global.json")
                .Add("dotnet", "src/**/*.csproj")
                .Add("Editorconfig", ".editorconfig")
                .Add("github-actions", ".github/actions/*.yml", ".github/workflows/*.yml")
                .Add("Markdown", "./**/*.md")
                .Add("Migration Script", "tools/MigrationScripts/**/*")
                .Add("npm", "./**/package-lock.json", "./**/package.json")
                .Add("Powershell", "./**/*.ps1", "./**/*.psm1")
                .Add("Read Me", "README.md")
                .Add("Setup", "SETUP.md")
                .Add("Solidity", "./**/*.sol")
                .Add("SQL", "./**/*.sql", "db/**/*")
                .Add("Static Code Analysis Rules", ".globalconfig", "src/CodeAnalysis.ruleset")
                .Add(
                    "Unit Tests",
                    "src/*.Tests.*/**/*",
                    "src/*.Tests.Integration.*/**/*",
                    "src/*.Tests.Integration/**/*",
                    "src/*.Tests/**/*"
                )
                .Build();

        Assert.Equal(expected: expected, actual: actual);
    }

    [Fact]
    public void BuildLabelsYmlWithTestsSuffixProjectHasTestColour()
    {
        (string labelsYaml, string labelerYaml) = this._labelsBuilder.BuildLabelsConfig(["Foo.Bar.Tests.csproj"]);

        this.Output.WriteLine(labelsYaml);
        this.Output.WriteLine(labelerYaml);

        string expectedLabels = new LabelsYamlBuilder()
            .Add("foo-bar-tests", "0e8a16", "Changes in Foo.Bar.Tests project")
            .Build();

        string expectedLabeler = new LabelerYamlBuilder().Add("foo-bar-tests", "src/Foo.Bar.Tests/**/*").Build();

        Assert.Contains(expectedLabels, labelsYaml, StringComparison.Ordinal);
        Assert.Contains(expectedLabeler, labelerYaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLabelsYmlWithTestsInNameProjectHasTestColour()
    {
        (string labelsYaml, string labelerYaml) = this._labelsBuilder.BuildLabelsConfig(["Foo.Tests.Mock.csproj"]);

        this.Output.WriteLine(labelsYaml);
        this.Output.WriteLine(labelerYaml);

        string expectedLabels = new LabelsYamlBuilder()
            .Add("foo-tests-mock", "0e8a16", "Changes in Foo.Tests.Mock project")
            .Build();

        string expectedLabeler = new LabelerYamlBuilder().Add("foo-tests-mock", "src/Foo.Tests.Mock/**/*").Build();

        Assert.Contains(expectedLabels, labelsYaml, StringComparison.Ordinal);
        Assert.Contains(expectedLabeler, labelerYaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLabelsYmlWithMocksSuffixProjectHasTestColour()
    {
        (string labelsYaml, string labelerYaml) = this._labelsBuilder.BuildLabelsConfig(["Foo.Bar.Mocks.csproj"]);

        this.Output.WriteLine(labelsYaml);
        this.Output.WriteLine(labelerYaml);

        string expectedLabels = new LabelsYamlBuilder()
            .Add("foo-bar-mocks", "0e8a16", "Changes in Foo.Bar.Mocks project")
            .Build();

        string expectedLabeler = new LabelerYamlBuilder().Add("foo-bar-mocks", "src/Foo.Bar.Mocks/**/*").Build();

        Assert.Contains(expectedLabels, labelsYaml, StringComparison.Ordinal);
        Assert.Contains(expectedLabeler, labelerYaml, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildLabelsYmlWithRegularProjectHasDefaultColour()
    {
        (string labelsYaml, string labelerYaml) = this._labelsBuilder.BuildLabelsConfig(["Foo.Bar.csproj"]);

        this.Output.WriteLine(labelsYaml);
        this.Output.WriteLine(labelerYaml);

        string expectedLabels = new LabelsYamlBuilder().Add("foo-bar", "96f7d2", "Changes in Foo.Bar project").Build();

        string expectedLabeler = new LabelerYamlBuilder().Add("foo-bar", "src/Foo.Bar/**/*").Build();

        Assert.Contains(expectedLabels, labelsYaml, StringComparison.Ordinal);
        Assert.Contains(expectedLabeler, labelerYaml, StringComparison.Ordinal);
    }
}
