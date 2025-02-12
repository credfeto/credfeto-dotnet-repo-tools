using Credfeto.DotNet.Repo.Tools.Release.Extensions;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Extensions;

// ShouldNeverAutoReleaseRepo Looks at ReleaseConfig.NeverRelease
public sealed partial class ReleaseConfigExtensionsTests
{
    [Theory]
    [InlineData("git@github.com:example/cs-template.git")]
    [InlineData("git@github.com:example/never-release.git")]
    public void ShouldNeverAutoReleaseRepo_ReturnsTrue(string repoUrl)
    {
        bool status = this._releaseConfig.ShouldNeverAutoReleaseRepo(repoUrl);
        Assert.True(condition: status, userMessage: "Should never release");
    }

    [Theory]
    [InlineData("git@github.com:example/release-me.git")]
    public void ShouldNeverAutoReleaseRepo_ReturnsFalse(string repoUrl)
    {
        bool status = this._releaseConfig.ShouldNeverAutoReleaseRepo(repoUrl);
        Assert.False(condition: status, userMessage: "Allow auto release");
    }
}
