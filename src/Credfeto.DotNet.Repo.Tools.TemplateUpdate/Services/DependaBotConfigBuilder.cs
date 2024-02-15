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

    public ValueTask<string> BuildDependabotConfigAsync(RepoContext repoContext, IReadOnlyList<PackageUpdate> packages, CancellationToken cancellationToken)
    {
        List<string> config = [
            "version: 2",
            "updates:"
            ];

        if (repoContext.HasSubModules())
        {
            AddBaseConfig(config: config, ecoSystem: "gitsubmodule", directory: "/", "submodule", "credfeto");
            AllowAllDependencies(config);
        }


        if (repoContext.HasDotNetFiles(out _, out _, out _))
        {
            AddDotNetConfig(config: config, packages: packages);
        }

        if(repoContext.HasNpmAndYarn(out IReadOnlyList<string>? directories))
        {
            foreach (string dir in directories)
            {
                AddBaseConfig(config: config, ecoSystem: "npm", directory: dir, "npm", "credfeto");
                config.Add("  versioning-strategy: increase-if-necessary");
                AllowAllDependencies(config);
            }
        }

        if(repoContext.HasDockerFiles())
        {
            AddBaseConfig(config: config, ecoSystem: "docker", directory: "/", "docker", "credfeto");
            AllowAllDependencies(config);
        }

        if (repoContext.HasNonStandardGithubActions())
        {
            AddBaseConfig(config: config, ecoSystem: "github-actions", directory: "/", "github-actions", "credfeto");
            AllowAllDependencies(config);
        }

        if(repoContext.HasPython())
        {
            AddBaseConfig(config: config, ecoSystem: "pip", directory: "/", "python", "credfeto");
            AllowAllDependencies(config);
        }



        return ValueTask.FromResult(string.Join(Environment.NewLine, config));
    }

    private static void AddDotNetConfig(List<string> config, IReadOnlyList<PackageUpdate> packages)
    {
        AddBaseConfig(config: config, ecoSystem: "github-actions", directory: "/", "nuget", "credfeto");
        AllowAllDependencies(config);

        if (packages is not [])
        {
            // Add packages to ignore
            config.Add("  ignore:");
            config.AddRange(packages.Select(package => package.ExactMatch
                                                ? $"  - dependency-name: \"{package.PackageId}\""
                                                : $"  - dependency-name: \"{package.PackageId}.*\""));
        }
    }

    private static void AllowAllDependencies(List<string> config)
    {
        // allow all packages
        config.AddRange([
            "  allow:",
            "  - dependency-type: all"
        ]);
    }

    private static void AddBaseConfig(List<string> config, string ecoSystem, string directory, string packageTypeLabel, string reviewer)
    {
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
            "  - \"Changelog Not Required\"",
        ]);
    }
}