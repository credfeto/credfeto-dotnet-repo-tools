using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class LabelsBuilder : ILabelsBuilder
{
    private static readonly IReadOnlyList<LabelConfig> BaseLabels =
    [
        new(Name: "C#",
            Description: "C# Source Files",
            Colour: "db6baa",
            [
                "./**/*.cs",
                "./**/*.csproj"
            ],
            []),
        new(Name: "C# Project", Description: "C# Project Files", Colour: "db6baa", ["./**/*.csproj"], []),
        new(Name: "C# Solution", Description: "C# Solutions", Colour: "db6baa", ["./**/*.sln"], []),
        new(Name: "Powershell",
            Description: "Powershell Source Files",
            Colour: "23bc12",
            [
                "./**/*.ps1",
                "./**/*.psm1"
            ],
            []),
        new(Name: "SQL",
            Description: "SQL Source Files",
            Colour: "413cd1",
            [
                "db/**/*",
                "./**/*.sql"
            ],
            []),
        new(Name: "Solidity", Description: "Solidity Source Files", Colour: "413cd1", ["./**/*.sol"], []),
        new(Name: "unit-tests",
            Description: "Unit test and integration test projects",
            Colour: "0e8a16",
            [
                "src/*.Tests.*/**/*",
                "src/*.Tests.Integration.*/**/*",
                "src/*.Tests/**/*",
                "src/*.Tests.Integration/**/*"
            ],
            []),
        new(Name: ".NET update", Description: "Update to .net global.json", Colour: "a870c9", ["src/global.json"], []),
        new(Name: "Config Change", Description: "Configuration files changes", Colour: "d8bb50", ["src/**/*.json"], ["src/global.json"]),
        new(Name: "Static Code Analysis Rules", Description: "Ruleset for static code analysis files", Colour: "00dead", ["src/CodeAnalysis.ruleset"], []),
        new(Name: "Migration Script", Description: "SQL Migration scripts", Colour: "b680e5", ["tools/MigrationScripts/**/*"], []),
        new(Name: "Legal Text", Description: "Legal text files", Colour: "facef0", ["tools/LegalText/**/*"], []),
        new(Name: "Change Log", Description: "Changelog tracking file", Colour: "53fcd4", ["CHANGELOG.md"], []),
        new(Name: "Read Me", Description: "Repository readme file", Colour: "5319e7", ["README.md"], []),
        new(Name: "Setup", Description: "Setup instructions", Colour: "5319e7", ["SETUP.md"], []),
        new(Name: "Markdown", Description: "Markdown files", Colour: "5319e7", ["./**/*.md"], []),
        new(Name: "github-actions", Description: "Github actions workflow files", Colour: "e09cf4", [".github/workflows/*.yml"], []),
        new(Name: "Tech Debt", Description: "Technical debt", Colour: "30027a", [], []),
        new(Name: "auto-pr", Description: "Pull request created automatically", Colour: "0000aa", [], []),
        new(Name: "no-pr-activity", Description: "Pull Request has had no activity for a long time", Colour: "ffff00", [], []),
        new(Name: "!!! WAITING FOR CLIENT PR", Description: "Pull request needs a client pull request to be merged at the same time", Colour: "ffff00", [], []),
        new(Name: "!!! WAITING FOR WALLET PR", Description: "Pull request needs a wallet pull request to be merged at the same time", Colour: "ffff00", [], []),
        new(Name: "!!! WAITING FOR SERVER PR", Description: "Pull request needs a server pull request to be merged at the same time", Colour: "ffff00", [], []),
        new(Name: "!!! WAITING FOR QA SIGNOFF", Description: "Pull request needs a QA Signoff before it can be merged", Colour: "ffff00", [], []),
        new(Name: "!!! WAITING FOR ETHEREUM PR", Description: "Pull request needs a server ethereum pull request to be merged at the same time", Colour: "ffff00", [], []),
        new(Name: "dependencies", Description: "Updates to dependencies", Colour: "0366d6", [], []),
        new(Name: "dotnet", Description: "Dotnet package updates", Colour: "db6baa", [], []),
        new(Name: "npm", Description: "npm package upate", Colour: "e99695", [], []),
        new(Name: "DO NOT MERGE", Description: "This pull request should not be merged yet", Colour: "ff0000", [], [])
    ];

    public (string labels, string labeler) BuildLabelsConfig(IReadOnlyList<string> projects)
    {
        if (projects is [])
        {
            return BuildFiles(BaseLabels);
        }

        List<LabelConfig> labels = [..BaseLabels];

        foreach (string fileName in projects)
        {
            string projectName = Path.GetFileNameWithoutExtension(fileName);
            string labelName = BuildLabelName(projectName);
            string colour = GetLabelColour(projectName);

            LabelConfig lc = new(Name: labelName, $"Changes in ${projectName} project", Colour: colour, [$"src/${projectName}/**/*"], []);

            labels.Add(lc);
        }

        return BuildFiles(labels);
    }

    private static string BuildLabelName(string projectName)
    {
        return projectName.Replace(oldChar: '.', newChar: '-')
                          .Replace(oldChar: ' ', newChar: '-')
                          .ToLowerInvariant();
    }

    private static string GetLabelColour(string name)
    {
        if (name.EndsWith(value: ".tests", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "0e8a16";
        }

        if (name.Contains(value: ".tests.", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "0e8a16";
        }

        if (name.EndsWith(value: ".mocks", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return "0e8a16";
        }

        return "96f7d2";
    }

    private static (string labels, string labeler) BuildFiles(IReadOnlyList<LabelConfig> labels)
    {
        IOrderedEnumerable<LabelConfig> sorted = labels.OrderBy(keySelector: x => x.Name, comparer: StringComparer.OrdinalIgnoreCase);

        StringBuilder labeller = new();
        StringBuilder labelsWithColour = new();

        foreach (LabelConfig group in sorted)
        {
            labeller = AddLabelMatch(labeller: labeller, group: group);
            labelsWithColour = AddLabelsWithColor(labelsWithColour: labelsWithColour, group: group);
        }

        return (labelsWithColour.ToString(), labeller.ToString());
    }

    private static StringBuilder AddLabelMatch(StringBuilder labeller, in LabelConfig group)
    {
        if (group is { Paths: [], PathsExclude: [] })
        {
            return labeller;
        }

        //Log -message "Adding group $groupName"

        IEnumerable<(bool Include, string Path)> paths = BuildIncludePaths(group)
            .Concat(BuildExcludePaths(group));

        string all = " - any: [ '" + string.Join(separator: ", ",
                                                 paths.Select(i => i.Include
                                                                  ? $"'{i.Path}'"
                                                                  : $"'!{i.Path}'")) + "' ]";

        //Log -message " - Adding Group with file match"
        return labeller.AppendLine($"\"{group.Name}\":")
                       .AppendLine(all);
    }

    private static IEnumerable<(bool Include, string Path)> BuildIncludePaths(in LabelConfig group)
    {
        return group.Paths.OrderBy(keySelector: x => x, comparer: StringComparer.OrdinalIgnoreCase)
                    .Select(path => (Include: true, Path: path));
    }

    private static IEnumerable<(bool Include, string Path)> BuildExcludePaths(in LabelConfig group)
    {
        return group.PathsExclude.OrderBy(keySelector: x => x, comparer: StringComparer.OrdinalIgnoreCase)
                    .Select(path => (Include: false, Path: path));
    }

    private static StringBuilder AddLabelsWithColor(StringBuilder labelsWithColour, in LabelConfig group)
    {
        //Log -message " - Adding Colour Group"
        labelsWithColour = labelsWithColour.AppendLine($" - name: \"{group.Name}\"")
                                           .AppendLine($"   color: \"{group.Colour}\"");

        if (!string.IsNullOrWhiteSpace(group.Description))
        {
            labelsWithColour = labelsWithColour.AppendLine($"   description: \"{group.Description}\"");
        }

        labelsWithColour = labelsWithColour.AppendLine();

        return labelsWithColour;
    }
}