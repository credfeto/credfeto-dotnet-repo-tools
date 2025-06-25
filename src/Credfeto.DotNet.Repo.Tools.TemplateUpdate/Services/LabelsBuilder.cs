using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class LabelsBuilder : ILabelsBuilder
{
    private static readonly IReadOnlyList<LabelConfig> BaseLabels =
    [
        new(Name: "C#", Description: "C# Source Files", Colour: "db6baa", ["./**/*.cs", "./**/*.csproj"], []),
        new(Name: "C# Project", Description: "C# Project Files", Colour: "db6baa", ["./**/*.csproj"], []),
        new(Name: "C# Solution", Description: "C# Solutions", Colour: "db6baa", ["./**/*.sln"], []),
        new(Name: "Powershell", Description: "Powershell Source Files", Colour: "23bc12", ["./**/*.ps1", "./**/*.psm1"], []),
        new(Name: "SQL", Description: "SQL Source Files", Colour: "413cd1", ["db/**/*", "./**/*.sql"], []),
        new(Name: "Solidity", Description: "Solidity Source Files", Colour: "413cd1", ["./**/*.sol"], []),
        new(Name: "Unit Tests",
            Description: "Unit test and integration test projects",
            Colour: "0e8a16",
            [
                "src/*.Tests.*/**/*",
                "src/*.Tests.Integration.*/**/*",
                "src/*.Tests/**/*",
                "src/*.Tests.Integration/**/*",
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
        new(Name: "dependencies", Description: "Updates to dependencies", Colour: "0366d6", [], []),
        new(Name: "dotnet", Description: "Dotnet package updates", Colour: "db6baa", [], []),
        new(Name: "npm", Description: "npm package update", Colour: "e99695", [], []),
        new(Name: "DO NOT MERGE", Description: "This pull request should not be merged yet", Colour: "ff0000", [], []),
    ];

    public LabelContent BuildLabelsConfig(IReadOnlyList<string> projects)
    {
        if (projects is [])
        {
            return BuildFiles(BaseLabels);
        }

        List<LabelConfig> labels = [.. BaseLabels];

        labels.AddRange(projects.Select(BuildLabelConfig));

        return BuildFiles(labels);
    }

    private static LabelConfig BuildLabelConfig(string fileName)
    {
        string projectName = Path.GetFileNameWithoutExtension(fileName);
        string labelName = BuildLabelName(projectName);
        string colour = GetLabelColour(projectName);

        LabelConfig lc = new(Name: labelName, $"Changes in {projectName} project", Colour: colour, [$"src/${projectName}/**/*"], []);

        return lc;
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

    private static LabelContent BuildFiles(IReadOnlyList<LabelConfig> labels)
    {
        IOrderedEnumerable<LabelConfig> sorted = labels.OrderBy(keySelector: x => x.Name, comparer: StringComparer.OrdinalIgnoreCase);

        StringBuilder labeller = new();
        StringBuilder labelsWithColour = new();

        foreach (LabelConfig labelConfig in sorted)
        {
            labeller = AddLabelMatch(labeller: labeller, labelConfig: labelConfig);
            labelsWithColour = AddLabelsWithColor(labelsWithColour: labelsWithColour, labelConfig: labelConfig);
        }

        return new(labelsWithColour.ToString(), labeller.ToString());
    }

    private static StringBuilder AddLabelMatch(StringBuilder labeller, in LabelConfig labelConfig)
    {
        if (labelConfig is { Paths: [], PathsExclude: [] })
        {
            return labeller;
        }

        //Log -message "Adding labelConfig $groupName"

        //Log -message " - Adding Group with file match"
        return labeller.AppendLine($"\"{labelConfig.Name}\":")
                       .AppendLine(BuildAllPathLine(labelConfig));
    }

    private static string BuildAllPathLine(in LabelConfig labelConfig)
    {
        IEnumerable<PathInfo> paths = BuildIncludePaths(labelConfig)
            .Concat(BuildExcludePaths(labelConfig));

        string all = " - any: [ " + string.Join(separator: ", ",
                                                paths.Select(i => i.Include
                                                                 ? $"'{i.Path}'"
                                                                 : $"'!{i.Path}'")) + " ]";

        return all;
    }

    private static IEnumerable<PathInfo> BuildIncludePaths(in LabelConfig labelConfig)
    {
        return labelConfig.Paths.Order(comparer: StringComparer.OrdinalIgnoreCase)
                          .Select(Create);

        static PathInfo Create(string path)
        {
            return new(Include: true, Path: path);
        }
    }

    private static IEnumerable<PathInfo> BuildExcludePaths(in LabelConfig labelConfig)
    {
        return labelConfig.PathsExclude.Order(comparer: StringComparer.OrdinalIgnoreCase)
                          .Select(Create);

        static PathInfo Create(string path)
        {
            return new(Include: false, Path: path);
        }
    }

    private static StringBuilder AddLabelsWithColor(StringBuilder labelsWithColour, in LabelConfig labelConfig)
    {
        //Log -message " - Adding Colour Group"
        labelsWithColour = labelsWithColour.AppendLine($" - name: \"{labelConfig.Name}\"")
                                           .AppendLine($"   color: \"{labelConfig.Colour}\"");

        if (!string.IsNullOrWhiteSpace(labelConfig.Description))
        {
            labelsWithColour = labelsWithColour.AppendLine($"   description: \"{labelConfig.Description}\"");
        }

        labelsWithColour = labelsWithColour.AppendLine();

        return labelsWithColour;
    }

    [DebuggerDisplay("Include: {Include}, Path: {Path}")]
    private readonly record struct PathInfo(bool Include, string Path);
}