using Credfeto.DotNet.Repo.Tools.Release.Extensions;
using Credfeto.DotNet.Repo.Tools.Release.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Extensions;

public sealed class ReleaseConfigExtensionsTests : TestBase
{
    private readonly ReleaseConfig _releaseConfig;

    public ReleaseConfigExtensionsTests()
    {
        this._releaseConfig = new(AutoReleasePendingPackages: 1,
                                  MinimumHoursBeforeAutoRelease: 5,
                                  InactivityHoursBeforeAutoRelease: 9,
                                  [
                                      new(Repo: "template", MatchType: MatchType.CONTAINS, Include: true),
                                      new(Repo: "git@github.com:example/never-release.git", MatchType: MatchType.EXACT, Include: true)
                                  ],
                                  [],
                                  []);
    }

    [Theory]
    [InlineData("git@github.com:credfeto/cs-template.git")]
    [InlineData("git@github.com:example/never-release.git")]
    public void ShouldNeverAutoReleaseRepo_ReturnsTrue(string repoUrl)
    {
        bool status = this._releaseConfig.ShouldNeverAutoReleaseRepo(repoUrl);
        Assert.True(condition: status, userMessage: "Should never release");
    }

    [Theory]
    [InlineData("git@github.com:credfeto/example1.git")]
    [InlineData("git@github.com:example/allow-release.git")]
    public void ShouldNeverAutoReleaseRepo_ReturnsFalse(string repoUrl)
    {
        bool status = this._releaseConfig.ShouldNeverAutoReleaseRepo(repoUrl);
        Assert.False(condition: status, userMessage: "Allow releases");
    }
}