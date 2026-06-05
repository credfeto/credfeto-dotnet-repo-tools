using System.Collections.Generic;
using System.Diagnostics;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Interfaces.Tests;

public sealed class ReleaseConfigTests : TestBase
{
    [Fact]
    public void MustBeAReadonlyRecordStruct()
    {
        Assert.True(typeof(ReleaseConfig).IsValueType, userMessage: $"{nameof(ReleaseConfig)} must be a value type");
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(typeof(ReleaseConfig).IsPublic, userMessage: $"{nameof(ReleaseConfig)} must be public");
    }

    [Fact]
    public void MustHaveDebuggerDisplayAttribute()
    {
        Assert.NotNull(typeof(ReleaseConfig).GetCustomAttributes(typeof(DebuggerDisplayAttribute), inherit: false));
    }

    [Fact]
    public void ConstructorSetsAutoReleasePendingPackages()
    {
        const int autoReleasePendingPackages = 3;

        ReleaseConfig config = new(
            AutoReleasePendingPackages: autoReleasePendingPackages,
            MinimumHoursBeforeAutoRelease: 1.0,
            InactivityHoursBeforeAutoRelease: 2.0,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );

        Assert.Equal(expected: autoReleasePendingPackages, actual: config.AutoReleasePendingPackages);
    }

    [Fact]
    public void ConstructorSetsMinimumHoursBeforeAutoRelease()
    {
        const double minimumHours = 4.5;

        ReleaseConfig config = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: minimumHours,
            InactivityHoursBeforeAutoRelease: 2.0,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );

        Assert.Equal(expected: minimumHours, actual: config.MinimumHoursBeforeAutoRelease);
    }

    [Fact]
    public void ConstructorSetsInactivityHoursBeforeAutoRelease()
    {
        const double inactivityHours = 8.0;

        ReleaseConfig config = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 1.0,
            InactivityHoursBeforeAutoRelease: inactivityHours,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );

        Assert.Equal(expected: inactivityHours, actual: config.InactivityHoursBeforeAutoRelease);
    }

    [Fact]
    public void ConstructorSetsNeverRelease()
    {
        IReadOnlyList<RepoMatch> neverRelease =
        [
            new(Repo: "git@github.com:example/never-release.git", MatchType: MatchType.EXACT, Include: true),
        ];

        ReleaseConfig config = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 1.0,
            InactivityHoursBeforeAutoRelease: 2.0,
            NeverRelease: neverRelease,
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );

        Assert.Same(expected: neverRelease, actual: config.NeverRelease);
    }

    [Fact]
    public void ConstructorSetsAllowedAutoUpgrade()
    {
        IReadOnlyList<RepoMatch> allowedAutoUpgrade =
        [
            new(Repo: "git@github.com:example/auto-upgrade.git", MatchType: MatchType.EXACT, Include: true),
        ];

        ReleaseConfig config = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 1.0,
            InactivityHoursBeforeAutoRelease: 2.0,
            NeverRelease: [],
            AllowedAutoUpgrade: allowedAutoUpgrade,
            AlwaysMatch: []
        );

        Assert.Same(expected: allowedAutoUpgrade, actual: config.AllowedAutoUpgrade);
    }

    [Fact]
    public void ConstructorSetsAlwaysMatch()
    {
        IReadOnlyList<RepoMatch> alwaysMatch = [new(Repo: "credfeto", MatchType: MatchType.CONTAINS, Include: true)];

        ReleaseConfig config = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 1.0,
            InactivityHoursBeforeAutoRelease: 2.0,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: alwaysMatch
        );

        Assert.Same(expected: alwaysMatch, actual: config.AlwaysMatch);
    }

    [Fact]
    public void TwoConfigsWithSameValuesAreEqual()
    {
        ReleaseConfig a = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5.0,
            InactivityHoursBeforeAutoRelease: 9.0,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );

        ReleaseConfig b = new(
            AutoReleasePendingPackages: 1,
            MinimumHoursBeforeAutoRelease: 5.0,
            InactivityHoursBeforeAutoRelease: 9.0,
            NeverRelease: [],
            AllowedAutoUpgrade: [],
            AlwaysMatch: []
        );

        Assert.Equal(a, b);
    }
}
