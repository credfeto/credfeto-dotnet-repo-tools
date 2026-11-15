using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public static class DependabotConfigWriter
{
    private const string UpdatesHeader = "updates:";

    public static List<string> StartConfig()
    {
        return ["---", "version: 2", UpdatesHeader];
    }

    public static string ToDocument(IReadOnlyList<string> config)
    {
        return string.Join(separator: Environment.NewLine, values: config) + Environment.NewLine;
    }

    public static void AddEcosystemSection(
        List<string> config,
        string ecoSystem,
        string directory,
        string packageTypeLabel,
        string reviewer
    )
    {
        AddBaseConfig(
            config: config,
            ecoSystem: ecoSystem,
            directory: directory,
            packageTypeLabel: packageTypeLabel,
            reviewer: reviewer
        );
        AllowAllDependencies(config);
    }

    public static void AddNpmSection(
        List<string> config,
        string directory,
        string packageTypeLabel,
        string reviewer,
        IReadOnlyList<DependabotGroup> groups
    )
    {
        AddBaseConfig(
            config: config,
            ecoSystem: "npm",
            directory: directory,
            packageTypeLabel: packageTypeLabel,
            reviewer: reviewer
        );
        config.Add("    versioning-strategy: increase-if-necessary");
        AllowAllDependencies(config);
        AddGroups(config: config, groups: groups);
    }

    public static void AddNuGetSection(
        List<string> config,
        string directory,
        string packageTypeLabel,
        string reviewer,
        IReadOnlyList<PackageUpdate> packages
    )
    {
        AddEcosystemSection(
            config: config,
            ecoSystem: "nuget",
            directory: directory,
            packageTypeLabel: packageTypeLabel,
            reviewer: reviewer
        );
        AddIgnoredPackages(config: config, packages: packages);
    }

    public static void AddBaseConfig(
        List<string> config,
        string ecoSystem,
        string directory,
        string packageTypeLabel,
        string reviewer
    )
    {
        if (!StringComparer.Ordinal.Equals(x: config[^1], y: UpdatesHeader))
        {
            config.Add("");
        }

        config.AddRange([
            $"  - package-ecosystem: {ecoSystem}",
            $"    directory: \"{directory}\"",
            "    schedule:",
            "      interval: daily",
            "      time: \"03:00\"",
            "      timezone: \"Europe/London\"",
            "    open-pull-requests-limit: 99",
            "    assignees:",
            $"      - {reviewer}",
            "    commit-message:",
            "      prefix: \"[Dependencies]\"",
            "    labels:",
            $"      - \"{packageTypeLabel}\"",
            "      - \"dependencies\"",
            "      - \"Changelog Not Required\"",
        ]);
    }

    public static void AllowAllDependencies(List<string> config)
    {
        config.AddRange(["    allow:", "      - dependency-type: all"]);
    }

    public static void AddIgnoredPackages(List<string> config, IReadOnlyList<PackageUpdate> packages)
    {
        if (packages is [])
        {
            return;
        }

        config.Add("    ignore:");

        IReadOnlyList<PackageUpdate> packagesToAdd =
        [
            .. DetermineMinimalDotnetPackages(packages)
                .OrderBy(keySelector: p => p.PackageId, comparer: StringComparer.OrdinalIgnoreCase),
        ];

        config.AddRange(
            packagesToAdd.Select(package =>
                package.ExactMatch
                    ? $"      - dependency-name: \"{package.PackageId}\""
                    : $"      - dependency-name: \"{package.PackageId}.*\""
            )
        );
    }

    public static void AddGroups(List<string> config, IReadOnlyList<DependabotGroup> groups)
    {
        if (groups is [])
        {
            return;
        }

        config.Add("    groups:");

        foreach (DependabotGroup group in groups)
        {
            config.Add($"      {group.Name}:");
            config.Add($"        applies-to: {group.AppliesTo}");
            config.Add("        patterns:");
            config.AddRange(group.Patterns.Select(pattern => $"          - \"{pattern}\""));
        }
    }

    [SuppressMessage(
        category: "Style",
        checkId: "IDE0028: Collection initialization can be simplified",
        Justification = "A collection expression cannot pass an IEqualityComparer to the HashSet constructor; simplifying would silently drop OrdinalIgnoreCase and change lookup semantics"
    )]
    private static IEnumerable<PackageUpdate> DetermineMinimalDotnetPackages(IReadOnlyList<PackageUpdate> packages)
    {
        List<string> wildcardPackages = [];

        foreach (
            PackageUpdate package in packages
                .Where(package => !package.ExactMatch)
                .OrderBy(package => package.PackageId.Length)
        )
        {
            if (wildcardPackages.Exists(candidate => IsWildcardMatch(package: package, wildcardPackage: candidate)))
            {
                continue;
            }

            wildcardPackages.Add(package.PackageId);

            yield return package;
        }

        HashSet<string> exactPackages = new(StringComparer.OrdinalIgnoreCase);

        foreach (
            PackageUpdate package in packages
                .Where(package => package.ExactMatch)
                .OrderBy(package => package.PackageId.Length)
        )
        {
            if (wildcardPackages.Exists(candidate => IsWildcardMatch(package: package, wildcardPackage: candidate)))
            {
                // Excluded by a wildcard
                continue;
            }

            if (!exactPackages.Add(package.PackageId))
            {
                // Excluded as already added
                continue;
            }

            yield return package;
        }
    }

    private static bool IsWildcardMatch(PackageUpdate package, string wildcardPackage)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(x: package.PackageId, y: wildcardPackage)
            || package.PackageId.StartsWith(wildcardPackage + ".", comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
