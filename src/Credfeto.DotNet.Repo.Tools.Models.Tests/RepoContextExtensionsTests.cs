using System.Collections.Generic;
using System.IO;
using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Models.Tests;

public sealed class RepoContextExtensionsTests : LoggingFolderCleanupTestBase
{
    public RepoContextExtensionsTests(ITestOutputHelper output)
        : base(output) { }

    private RepoContext CreateContext(IGitRepository? repository = null, string? workingDirectory = null)
    {
        return new(
            ClonePath: this.TempFolder,
            Repository: repository ?? GetSubstitute<IGitRepository>(),
            WorkingDirectory: workingDirectory ?? this.TempFolder,
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasSubModulesDelegatesToRepository(bool hasSubmodules)
    {
        IGitRepository repository = GetSubstitute<IGitRepository>();
        repository.HasSubmodules.Returns(hasSubmodules);

        RepoContext context = this.CreateContext(repository);

        Assert.Equal(expected: hasSubmodules, actual: context.HasSubModules());
    }

    [Fact]
    public void HasDockerFilesReturnsFalseWhenNoDockerfilePresent()
    {
        RepoContext context = this.CreateContext();

        Assert.False(condition: context.HasDockerFiles(), userMessage: "Should not have docker files");
    }

    [Fact]
    public void HasDockerFilesReturnsTrueWhenDockerfilePresent()
    {
        File.WriteAllText(path: Path.Combine(path1: this.TempFolder, path2: "Dockerfile"), contents: "FROM scratch");

        RepoContext context = this.CreateContext();

        Assert.True(condition: context.HasDockerFiles(), userMessage: "Should have docker files");
    }

    [Fact]
    public void HasPythonReturnsFalseWhenNoRequirementsPresent()
    {
        RepoContext context = this.CreateContext();

        Assert.False(condition: context.HasPython(), userMessage: "Should not have python");
    }

    [Fact]
    public void HasPythonReturnsTrueWhenRequirementsPresent()
    {
        File.WriteAllText(path: Path.Combine(path1: this.TempFolder, path2: "requirements.txt"), contents: "");

        RepoContext context = this.CreateContext();

        Assert.True(condition: context.HasPython(), userMessage: "Should have python");
    }

    [Fact]
    public void HasNonStandardGithubActionsReturnsFalseWhenRepoDirectoryAbsent()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "repo");
        string templateDir = Path.Combine(path1: this.TempFolder, path2: "template");
        Directory.CreateDirectory(repoDir);

        RepoContext context = this.CreateContext(workingDirectory: repoDir);

        Assert.False(
            condition: context.HasNonStandardGithubActions(templateDir),
            userMessage: "Should not have non-standard actions when repo has no .github folder"
        );
    }

    [Fact]
    public void HasNonStandardGithubActionsReturnsTrueWhenTemplateDirectoryAbsent()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "repo");
        string templateDir = Path.Combine(path1: this.TempFolder, path2: "template");
        string repoActionsDir = Path.Combine(repoDir, ".github", "actions");
        Directory.CreateDirectory(repoActionsDir);
        File.WriteAllText(path: Path.Combine(path1: repoActionsDir, path2: "custom.yml"), contents: "name: custom");

        RepoContext context = this.CreateContext(workingDirectory: repoDir);

        Assert.True(
            condition: context.HasNonStandardGithubActions(templateDir),
            userMessage: "Should have non-standard actions when template has no .github folder"
        );
    }

    [Fact]
    public void HasNonStandardGithubActionsReturnsTrueWhenRepoHasExtraFiles()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "repo");
        string templateDir = Path.Combine(path1: this.TempFolder, path2: "template");
        string repoActionsDir = Path.Combine(repoDir, ".github", "actions");
        string templateActionsDir = Path.Combine(templateDir, ".github", "actions");
        Directory.CreateDirectory(repoActionsDir);
        Directory.CreateDirectory(templateActionsDir);
        File.WriteAllText(path: Path.Combine(path1: repoActionsDir, path2: "common.yml"), contents: "name: common");
        File.WriteAllText(path: Path.Combine(path1: templateActionsDir, path2: "common.yml"), contents: "name: common");
        File.WriteAllText(path: Path.Combine(path1: repoActionsDir, path2: "extra.yml"), contents: "name: extra");

        RepoContext context = this.CreateContext(workingDirectory: repoDir);

        Assert.True(
            condition: context.HasNonStandardGithubActions(templateDir),
            userMessage: "Should have non-standard actions when repo has files not present in template"
        );
    }

    [Fact]
    public void HasNonStandardGithubActionsReturnsFalseWhenNoExtraFiles()
    {
        string repoDir = Path.Combine(path1: this.TempFolder, path2: "repo");
        string templateDir = Path.Combine(path1: this.TempFolder, path2: "template");
        string repoActionsDir = Path.Combine(repoDir, ".github", "actions");
        string templateActionsDir = Path.Combine(templateDir, ".github", "actions");
        Directory.CreateDirectory(repoActionsDir);
        Directory.CreateDirectory(templateActionsDir);
        File.WriteAllText(path: Path.Combine(path1: repoActionsDir, path2: "common.yml"), contents: "name: common");
        File.WriteAllText(path: Path.Combine(path1: templateActionsDir, path2: "common.yml"), contents: "name: common");

        RepoContext context = this.CreateContext(workingDirectory: repoDir);

        Assert.False(
            condition: context.HasNonStandardGithubActions(templateDir),
            userMessage: "Should not have non-standard actions when repo matches template"
        );
    }

    [Fact]
    public void HasNpmAndYarnReturnsFalseWhenNoPackageJsonPresent()
    {
        RepoContext context = this.CreateContext();

        bool result = context.HasNpmAndYarn(out IReadOnlyList<string>? directories);

        Assert.False(condition: result, userMessage: "Should not have npm/yarn");
        Assert.Null(directories);
    }

    [Fact]
    public void HasNpmAndYarnReturnsTrueWithDirectoriesWhenPackageJsonPresent()
    {
        string appDir = Path.Combine(path1: this.TempFolder, path2: "app");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(path: Path.Combine(path1: appDir, path2: "package.json"), contents: "{}");

        RepoContext context = this.CreateContext();

        bool result = context.HasNpmAndYarn(out IReadOnlyList<string>? directories);

        Assert.True(condition: result, userMessage: "Should have npm/yarn");
        Assert.NotNull(directories);
        Assert.Contains(expected: appDir[this.TempFolder.Length..], collection: directories);
    }
}
