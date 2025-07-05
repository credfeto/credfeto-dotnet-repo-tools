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

        const string expected =
            " - name: \".NET update\"\n   color: \"a870c9\"\n   description: \"Update to .net global.json\"\n\n - name: \"auto-pr\"\n   color: \"0000aa\"\n   description: \"Pull request created automatically\"\n\n - name: \"C#\"\n   color: \"db6baa\"\n   description: \"C# Source Files\"\n\n - name: \"C# Project\"\n   color: \"db6baa\"\n   description: \"C# Project Files\"\n\n - name: \"C# Solution\"\n   color: \"db6baa\"\n   description: \"C# Solutions\"\n\n - name: \"Change Log\"\n   color: \"53fcd4\"\n   description: \"Changelog tracking file\"\n\n - name: \"Config Change\"\n   color: \"d8bb50\"\n   description: \"Configuration files changes\"\n\n - name: \"dependencies\"\n   color: \"0366d6\"\n   description: \"Updates to dependencies\"\n\n - name: \"DO NOT MERGE\"\n   color: \"ff0000\"\n   description: \"This pull request should not be merged yet\"\n\n - name: \"dotnet\"\n   color: \"db6baa\"\n   description: \"Dotnet package updates\"\n\n - name: \"github-actions\"\n   color: \"e09cf4\"\n   description: \"Github actions workflow files\"\n\n - name: \"Markdown\"\n   color: \"5319e7\"\n   description: \"Markdown files\"\n\n - name: \"Migration Script\"\n   color: \"b680e5\"\n   description: \"SQL Migration scripts\"\n\n - name: \"no-pr-activity\"\n   color: \"ffff00\"\n   description: \"Pull Request has had no activity for a long time\"\n\n - name: \"npm\"\n   color: \"e99695\"\n   description: \"npm package update\"\n\n - name: \"Powershell\"\n   color: \"23bc12\"\n   description: \"Powershell Source Files\"\n\n - name: \"Read Me\"\n   color: \"5319e7\"\n   description: \"Repository readme file\"\n\n - name: \"Setup\"\n   color: \"5319e7\"\n   description: \"Setup instructions\"\n\n - name: \"Solidity\"\n   color: \"413cd1\"\n   description: \"Solidity Source Files\"\n\n - name: \"SQL\"\n   color: \"413cd1\"\n   description: \"SQL Source Files\"\n\n - name: \"Static Code Analysis Rules\"\n   color: \"00dead\"\n   description: \"Ruleset for static code analysis files\"\n\n - name: \"Tech Debt\"\n   color: \"30027a\"\n   description: \"Technical debt\"\n\n - name: \"Unit Tests\"\n   color: \"0e8a16\"\n   description: \"Unit test and integration test projects\"\n\n";
        Assert.Equal(expected: expected, actual: actual);
    }

    [Fact]
    public void BuildLabelerYmlNoProjects()
    {
        (string _, string actual) = this._labelsBuilder.BuildLabelsConfig([]);

        this.Output.WriteLine(actual);

        const string expected =
            "\".NET update\":\n - any: [ 'src/global.json' ]\n\"C#\":\n - any: [ './**/*.cs', './**/*.csproj' ]\n\"C# Project\":\n - any: [ './**/*.csproj' ]\n\"C# Solution\":\n - any: [ './**/*.sln' ]\n\"Change Log\":\n - any: [ 'CHANGELOG.md' ]\n\"Config Change\":\n - any: [ 'src/**/*.json', '!src/global.json' ]\n\"github-actions\":\n - any: [ '.github/workflows/*.yml' ]\n\"Markdown\":\n - any: [ './**/*.md' ]\n\"Migration Script\":\n - any: [ 'tools/MigrationScripts/**/*' ]\n\"Powershell\":\n - any: [ './**/*.ps1', './**/*.psm1' ]\n\"Read Me\":\n - any: [ 'README.md' ]\n\"Setup\":\n - any: [ 'SETUP.md' ]\n\"Solidity\":\n - any: [ './**/*.sol' ]\n\"SQL\":\n - any: [ './**/*.sql', 'db/**/*' ]\n\"Static Code Analysis Rules\":\n - any: [ 'src/CodeAnalysis.ruleset' ]\n\"Unit Tests\":\n - any: [ 'src/*.Tests.*/**/*', 'src/*.Tests.Integration.*/**/*', 'src/*.Tests.Integration/**/*', 'src/*.Tests/**/*' ]\n";
        Assert.Equal(expected: expected, actual: actual);
    }
}
