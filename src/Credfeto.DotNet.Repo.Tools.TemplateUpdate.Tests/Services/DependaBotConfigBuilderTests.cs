using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using Credfeto.DotNet.Repo.Tools.Models;
using Credfeto.DotNet.Repo.Tools.Models.Packages;
using Credfeto.DotNet.Repo.Tools.TemplateUpdate.Services;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests.Services;

public sealed class DependaBotConfigBuilderTests : LoggingTestBase, IDisposable
{
    private readonly string _tempFolder;
    private readonly IDependaBotConfigBuilder _dependaBotConfigBuilder;

    public DependaBotConfigBuilderTests(ITestOutputHelper output)
        : base(output)
    {
        this._tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(this._tempFolder);

        this._dependaBotConfigBuilder = new DependaBotConfigBuilder(this.GetTypedLogger<DependaBotConfigBuilder>());
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempFolder))
        {
            Directory.Delete(path: this._tempFolder, recursive: true);
        }
    }

    [Fact]
    public async Task NoDependenciesGeneratesMinimalConfig()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: EmptyDotNetFiles(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("version: 2", result, StringComparison.Ordinal);
        Assert.Contains("updates:", result, StringComparison.Ordinal);
        Assert.DoesNotContain("package-ecosystem:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithDotNetSolutionAndNoPackagesGeneratesNuGetSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: DotNetFilesWithSolutionsAndProjects(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: nuget", result, StringComparison.Ordinal);
        Assert.DoesNotContain("ignore:", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithDotNetAndWildcardPackageGeneratesIgnoreSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: DotNetFilesWithSolutionsAndProjects(),
            packages: [WildcardPackage("FunFair")],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: nuget", result, StringComparison.Ordinal);
        Assert.Contains("ignore:", result, StringComparison.Ordinal);
        Assert.Contains("dependency-name: \"FunFair.*\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithDotNetAndExactPackageGeneratesIgnoreSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: DotNetFilesWithSolutionsAndProjects(),
            packages: [ExactPackage("FunFair.Test.Common")],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: nuget", result, StringComparison.Ordinal);
        Assert.Contains("ignore:", result, StringComparison.Ordinal);
        Assert.Contains("dependency-name: \"FunFair.Test.Common\"", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\"FunFair.Test.Common.*\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithSubmodulesGeneratesGitSubmoduleSection()
    {
        IGitRepository repository = this.CreateRepository(hasSubmodules: true);
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: EmptyDotNetFiles(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: gitsubmodule", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithDockerFileGeneratesDockerSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");
        await File.WriteAllTextAsync(
            path: Path.Combine(this._tempFolder, "Dockerfile"),
            contents: "FROM ubuntu:latest",
            cancellationToken: this.CancellationToken()
        );

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: EmptyDotNetFiles(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: docker", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithPythonFileGeneratesPipSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");
        await File.WriteAllTextAsync(
            path: Path.Combine(this._tempFolder, "requirements.txt"),
            contents: "requests==2.28.0",
            cancellationToken: this.CancellationToken()
        );

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: EmptyDotNetFiles(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: pip", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNpmFileGeneratesNpmSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");
        await File.WriteAllTextAsync(
            path: Path.Combine(this._tempFolder, "package.json"),
            contents: "{}",
            cancellationToken: this.CancellationToken()
        );

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: this._tempFolder,
            dotNetFiles: EmptyDotNetFiles(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: npm", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNonStandardGithubActionsGeneratesGithubActionsSection()
    {
        IGitRepository repository = this.CreateRepository();
        RepoContext repoContext = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        string actionsDir = Path.Combine(this._tempFolder, ".github", "actions");
        Directory.CreateDirectory(actionsDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(actionsDir, "custom.yml"),
            contents: "name: custom action",
            cancellationToken: this.CancellationToken()
        );

        string templateFolder = Path.Combine(this._tempFolder, "template");
        Directory.CreateDirectory(templateFolder);

        string result = await this._dependaBotConfigBuilder.BuildDependabotConfigAsync(
            repoContext: repoContext,
            templateFolder: templateFolder,
            dotNetFiles: EmptyDotNetFiles(),
            packages: [],
            cancellationToken: this.CancellationToken()
        );

        this.Output.WriteLine(result);
        Assert.Contains("- package-ecosystem: github-actions", result, StringComparison.Ordinal);
    }

    private IGitRepository CreateRepository(bool hasSubmodules = false)
    {
        IGitRepository repository = GetSubstitute<IGitRepository>();
        repository.ClonePath.Returns("git@github.com:test/test.git");
        repository.WorkingDirectory.Returns(this._tempFolder);
        repository.GetDefaultBranch(GitConstants.Upstream).Returns("main");
        repository.HasSubmodules.Returns(hasSubmodules);

        return repository;
    }

    private static DotNetFiles EmptyDotNetFiles()
    {
        return new DotNetFiles(SourceDirectory: "/src", Solutions: [], Projects: []);
    }

    private static DotNetFiles DotNetFilesWithSolutionsAndProjects()
    {
        return new DotNetFiles(SourceDirectory: "/src", Solutions: ["Test.sln"], Projects: ["src/Test/Test.csproj"]);
    }

    private static PackageUpdate WildcardPackage(string packageId)
    {
        return new PackageUpdate(
            packageId: packageId,
            packageType: "all",
            exactMatch: false,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );
    }

    private static PackageUpdate ExactPackage(string packageId)
    {
        return new PackageUpdate(
            packageId: packageId,
            packageType: "all",
            exactMatch: true,
            versionBumpPackage: false,
            prohibitVersionBumpWhenReferenced: false,
            exclude: null
        );
    }
}
