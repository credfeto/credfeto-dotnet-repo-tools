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

        string expected = new LabelsYamlBuilder()
            .Add(".NET update", "a870c9", "Update to .net global.json")
            .Add("AI-Work", "ffa500", "Work for an AI Agent")
            .Add("auto-pr", "0000aa", "Pull request created automatically")
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
            .Add("no-pr-activity", "ffff00", "Pull Request has had no activity for a long time")
            .Add("npm", "e99695", "npm package update")
            .Add("On Hold", "ff0000", "Do not work on this")
            .Add("Performance", "0075ca", "Performance enhancement or issue")
            .Add("Powershell", "23bc12", "Powershell Source Files")
            .Add("Read Me", "5319e7", "Repository readme file")
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

        string expected = new LabelerYamlBuilder()
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
            .Add("Unit Tests", "src/*.Tests.*/**/*", "src/*.Tests.Integration.*/**/*", "src/*.Tests.Integration/**/*", "src/*.Tests/**/*")
            .Build();

        Assert.Equal(expected: expected, actual: actual);
    }
}
