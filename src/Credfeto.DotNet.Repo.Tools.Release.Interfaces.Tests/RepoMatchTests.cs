using System.Diagnostics;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces.Tests;

public sealed class RepoMatchTests : TestBase
{
    [Fact]
    public void MustBeAReadonlyRecordStruct()
    {
        Assert.True(typeof(RepoMatch).IsValueType, userMessage: $"{nameof(RepoMatch)} must be a value type");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(RepoMatch).IsPublic, userMessage: $"{nameof(RepoMatch)} must be public");
    }

    [Fact]
    public void MustHaveDebuggerDisplayAttribute()
    {
        Assert.NotNull(typeof(RepoMatch).GetCustomAttributes(typeof(DebuggerDisplayAttribute), inherit: false));
    }

    [Fact]
    public void ConstructorSetsRepo()
    {
        const string repo = "git@github.com:example/test.git";

        RepoMatch match = new(Repo: repo, MatchType: MatchType.EXACT, Include: true);

        Assert.Equal(expected: repo, actual: match.Repo);
    }

    [Fact]
    public void ConstructorSetsMatchType()
    {
        RepoMatch match = new(Repo: "test", MatchType: MatchType.CONTAINS, Include: true);

        Assert.Equal(expected: MatchType.CONTAINS, actual: match.MatchType);
    }

    [Fact]
    public void ConstructorSetsIncludeToFalse()
    {
        RepoMatch match = new(Repo: "test", MatchType: MatchType.EXACT, Include: false);

        Assert.False(match.Include, userMessage: "Include should be false");
    }

    [Fact]
    public void IsMatch_ExactMatch_SameCase_ReturnsTrue()
    {
        RepoMatch match = new(Repo: "git@github.com:example/test.git", MatchType: MatchType.EXACT, Include: true);

        Assert.True(match.IsMatch("git@github.com:example/test.git"), userMessage: "Exact same-case should match");
    }

    [Fact]
    public void IsMatch_ExactMatch_DifferentCase_ReturnsTrue()
    {
        RepoMatch match = new(Repo: "git@github.com:example/test.git", MatchType: MatchType.EXACT, Include: true);

        Assert.True(match.IsMatch("GIT@GITHUB.COM:EXAMPLE/TEST.GIT"), userMessage: "Exact different-case should match");
    }

    [Fact]
    public void IsMatch_ExactMatch_DifferentRepo_ReturnsFalse()
    {
        RepoMatch match = new(Repo: "git@github.com:example/test.git", MatchType: MatchType.EXACT, Include: true);

        Assert.False(match.IsMatch("git@github.com:example/other.git"), userMessage: "Different repo should not match");
    }

    [Fact]
    public void IsMatch_ContainsMatch_SubstringPresent_ReturnsTrue()
    {
        RepoMatch match = new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true);

        Assert.True(
            match.IsMatch("git@github.com:credfeto/some-repo.git"),
            userMessage: "Substring present should match"
        );
    }

    [Fact]
    public void IsMatch_ContainsMatch_SubstringPresentDifferentCase_ReturnsTrue()
    {
        RepoMatch match = new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true);

        Assert.True(
            match.IsMatch("git@github.com:CREDFETO/some-repo.git"),
            userMessage: "Substring present different case should match"
        );
    }

    [Fact]
    public void IsMatch_ContainsMatch_SubstringAbsent_ReturnsFalse()
    {
        RepoMatch match = new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true);

        Assert.False(
            match.IsMatch("git@github.com:example/other-repo.git"),
            userMessage: "Substring absent should not match"
        );
    }

    [Fact]
    public void TwoMatchesWithSameValuesAreEqual()
    {
        RepoMatch a = new(Repo: "git@github.com:example/test.git", MatchType: MatchType.EXACT, Include: true);
        RepoMatch b = new(Repo: "git@github.com:example/test.git", MatchType: MatchType.EXACT, Include: true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoMatchesWithDifferentReposAreNotEqual()
    {
        RepoMatch a = new(Repo: "git@github.com:example/test.git", MatchType: MatchType.EXACT, Include: true);
        RepoMatch b = new(Repo: "git@github.com:example/other.git", MatchType: MatchType.EXACT, Include: true);

        Assert.NotEqual(a, b);
    }
}
