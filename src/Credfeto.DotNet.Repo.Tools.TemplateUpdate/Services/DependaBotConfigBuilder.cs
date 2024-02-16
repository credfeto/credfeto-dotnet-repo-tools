using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class DependaBotConfigBuilder : IDependaBotConfigBuilder
{
    private readonly ILogger<DependaBotConfigBuilder> _logger;

    public DependaBotConfigBuilder(ILogger<DependaBotConfigBuilder> logger)
    {
        this._logger = logger;
    }

    public ValueTask<string> BuildDependabotConfigAsync(RepoContext repoContext, string templateFolder, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        List<string> config =
        [
            "version: 2",
            "updates:"
        ];

        if (repoContext.HasSubModules())
        {
            this.AddBaseConfig(config: config, ecoSystem: "gitsubmodule", directory: "/", packageTypeLabel: "submodule", reviewer: "credfeto");
            AllowAllDependencies(config);
        }

        if (repoContext.HasDotNetFiles(sourceDirectory: out _, solutions: out _, projects: out _))
        {
            this.AddDotNetConfig(config: config, packages: packages);
        }

        if (repoContext.HasNpmAndYarn(out IReadOnlyList<string>? directories))
        {
            foreach (string dir in directories)
            {
                this.AddBaseConfig(config: config, ecoSystem: "npm", directory: dir, packageTypeLabel: "npm", reviewer: "credfeto");
                config.Add("  versioning-strategy: increase-if-necessary");
                AllowAllDependencies(config);
            }
        }

        if (repoContext.HasDockerFiles())
        {
            this.AddBaseConfig(config: config, ecoSystem: "docker", directory: "/", packageTypeLabel: "docker", reviewer: "credfeto");
            AllowAllDependencies(config);
        }

        if (repoContext.HasNonStandardGithubActions(templateFolder))
        {
            this.AddBaseConfig(config: config, ecoSystem: "github-actions", directory: "/", packageTypeLabel: "github-actions", reviewer: "credfeto");
            AllowAllDependencies(config);
        }

        if (repoContext.HasPython())
        {
            this.AddBaseConfig(config: config, ecoSystem: "pip", directory: "/", packageTypeLabel: "python", reviewer: "credfeto");
            AllowAllDependencies(config);
        }

        return ValueTask.FromResult(string.Join(separator: Environment.NewLine, values: config));
    }

    private void AddDotNetConfig(List<string> config, IReadOnlyList<PackageUpdate> packages)
    {
        this.AddBaseConfig(config: config, ecoSystem: "github-actions", directory: "/", packageTypeLabel: "nuget", reviewer: "credfeto");
        AllowAllDependencies(config);

        if (packages is [])
        {
            return;
        }

        // Add packages to ignore
        config.Add("  ignore:");
        config.AddRange(packages.Select(package => package.ExactMatch
                                            ? $"  - dependency-name: \"{package.PackageId}\""
                                            : $"  - dependency-name: \"{package.PackageId}.*\""));
    }

    private static void AllowAllDependencies(List<string> config)
    {
        // allow all packages
        config.AddRange([
            "  allow:",
            "  - dependency-type: all"
        ]);
    }

    private void AddBaseConfig(List<string> config, string ecoSystem, string directory, string packageTypeLabel, string reviewer)
    {
        this._logger.LogInformation($"Adding Dependabot config for {ecoSystem} in {directory}");

        config.AddRange([
            "",
            $"- package-ecosystem: {ecoSystem}",
            $"  directory: \"{directory}\"",
            "  schedule:",
            "    interval: daily",
            "    time: \"03:00\"",
            "    timezone: \"Europe/London\"",
            "  open-pull-requests-limit: 99",
            "  reviewers:",
            $"  - {reviewer}",
            "  assignees:",
            $"  - {reviewer}",
            "  commit-message:",
            "    prefix: \"[Dependencies]\"",
            "  labels:",
            $"  - \"{packageTypeLabel}\"",
            "  - \"dependencies\"",
            "  - \"Changelog Not Required\""
        ]);
    }
}