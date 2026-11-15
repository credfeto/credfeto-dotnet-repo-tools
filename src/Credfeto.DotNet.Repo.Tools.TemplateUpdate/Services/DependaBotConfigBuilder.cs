using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Models;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;

public sealed class DependaBotConfigBuilder : IDependaBotConfigBuilder
{
    private const string Reviewer = "credfeto";
    private const string RootDirectory = "/";

    private static readonly IReadOnlyList<string> VitestPatterns = ["vitest", "@vitest/*"];

    private static readonly IReadOnlyList<DependabotGroup> NpmGroups =
    [
        new(Name: "vitest", AppliesTo: "version-updates", Patterns: VitestPatterns),
        new(Name: "vitest-security", AppliesTo: "security-updates", Patterns: VitestPatterns),
    ];

    private readonly ILogger<DependaBotConfigBuilder> _logger;

    public DependaBotConfigBuilder(ILogger<DependaBotConfigBuilder> logger)
    {
        this._logger = logger;
    }

    public ValueTask<string> BuildDependabotConfigAsync(
        RepoContext repoContext,
        string templateFolder,
        DotNetFiles dotNetFiles,
        IReadOnlyList<PackageUpdate> packages,
        CancellationToken cancellationToken
    )
    {
        List<string> config = DependabotConfigWriter.StartConfig();

        if (repoContext.HasSubModules())
        {
            this.AddEcosystemSection(config: config, ecoSystem: "gitsubmodule", packageTypeLabel: "submodule");
        }

        if (dotNetFiles.HasSolutionsAndProjects)
        {
            this.AddNuGetSection(config: config, packages: packages);
        }

        if (repoContext.HasNpmAndYarn(out IReadOnlyList<string>? directories))
        {
            foreach (string dir in directories)
            {
                this.AddNpmSection(config: config, directory: dir);
            }
        }

        if (repoContext.HasDockerFiles())
        {
            this.AddEcosystemSection(config: config, ecoSystem: "docker", packageTypeLabel: "docker");
        }

        if (repoContext.HasNonStandardGithubActions(templateFolder))
        {
            this.AddEcosystemSection(config: config, ecoSystem: "github-actions", packageTypeLabel: "github-actions");
        }

        if (repoContext.HasPython())
        {
            this.AddEcosystemSection(config: config, ecoSystem: "pip", packageTypeLabel: "python");
        }

        return ValueTask.FromResult(DependabotConfigWriter.ToDocument(config));
    }

    private void AddEcosystemSection(List<string> config, string ecoSystem, string packageTypeLabel)
    {
        this._logger.LogAddingConfigForEcosystem(ecoSystem: ecoSystem, directory: RootDirectory);

        DependabotConfigWriter.AddEcosystemSection(
            config: config,
            ecoSystem: ecoSystem,
            directory: RootDirectory,
            packageTypeLabel: packageTypeLabel,
            reviewer: Reviewer
        );
    }

    private void AddNuGetSection(List<string> config, IReadOnlyList<PackageUpdate> packages)
    {
        this._logger.LogAddingConfigForEcosystem(ecoSystem: "nuget", directory: RootDirectory);

        DependabotConfigWriter.AddNuGetSection(
            config: config,
            directory: RootDirectory,
            packageTypeLabel: "nuget",
            reviewer: Reviewer,
            packages: packages
        );
    }

    private void AddNpmSection(List<string> config, string directory)
    {
        this._logger.LogAddingConfigForEcosystem(ecoSystem: "npm", directory: directory);

        DependabotConfigWriter.AddNpmSection(
            config: config,
            directory: directory,
            packageTypeLabel: "npm",
            reviewer: Reviewer,
            groups: NpmGroups
        );
    }
}
