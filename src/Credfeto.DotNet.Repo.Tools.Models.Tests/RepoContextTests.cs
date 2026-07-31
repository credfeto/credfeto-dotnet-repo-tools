using Credfeto.DotNet.Repo.Tools.Git.Interfaces;
using FunFair.Test.Common;
using NSubstitute;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Models.Tests;

public sealed class RepoContextTests : TestBase
{
    [Fact]
    public void PrimaryConstructorSetsAllProperties()
    {
        IGitRepository repository = GetSubstitute<IGitRepository>();

        RepoContext context = new(
            ClonePath: "/tmp/clone",
            Repository: repository,
            WorkingDirectory: "/tmp/work",
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );

        Assert.Equal(expected: "/tmp/clone", actual: context.ClonePath);
        Assert.Same(expected: repository, actual: context.Repository);
        Assert.Equal(expected: "/tmp/work", actual: context.WorkingDirectory);
        Assert.Equal(expected: "main", actual: context.DefaultBranch);
        Assert.Equal(expected: "CHANGELOG.md", actual: context.ChangeLogFileName);
    }

    [Fact]
    public void SecondaryConstructorDerivesFieldsFromRepository()
    {
        IGitRepository repository = GetSubstitute<IGitRepository>();
        repository.ClonePath.Returns("/tmp/clone");
        repository.WorkingDirectory.Returns("/tmp/work");
        repository.GetDefaultBranch(GitConstants.Upstream).Returns("main");

        RepoContext context = new(Repository: repository, ChangeLogFileName: "CHANGELOG.md");

        Assert.Equal(expected: "/tmp/clone", actual: context.ClonePath);
        Assert.Same(expected: repository, actual: context.Repository);
        Assert.Equal(expected: "/tmp/work", actual: context.WorkingDirectory);
        Assert.Equal(expected: "main", actual: context.DefaultBranch);
        Assert.Equal(expected: "CHANGELOG.md", actual: context.ChangeLogFileName);
    }

    [Fact]
    public void InstancesWithIdenticalValuesAreEqual()
    {
        IGitRepository repository = GetSubstitute<IGitRepository>();

        RepoContext first = new(
            ClonePath: "/tmp/clone",
            Repository: repository,
            WorkingDirectory: "/tmp/work",
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );
        RepoContext second = new(
            ClonePath: "/tmp/clone",
            Repository: repository,
            WorkingDirectory: "/tmp/work",
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );

        Assert.Equal(expected: first, actual: second);
    }

    [Fact]
    public void InstancesWithDifferingValuesAreNotEqual()
    {
        IGitRepository repository = GetSubstitute<IGitRepository>();

        RepoContext first = new(
            ClonePath: "/tmp/clone",
            Repository: repository,
            WorkingDirectory: "/tmp/work",
            DefaultBranch: "main",
            ChangeLogFileName: "CHANGELOG.md"
        );
        RepoContext second = new(
            ClonePath: "/tmp/clone",
            Repository: repository,
            WorkingDirectory: "/tmp/work",
            DefaultBranch: "develop",
            ChangeLogFileName: "CHANGELOG.md"
        );

        Assert.NotEqual(expected: first, actual: second);
    }
}
