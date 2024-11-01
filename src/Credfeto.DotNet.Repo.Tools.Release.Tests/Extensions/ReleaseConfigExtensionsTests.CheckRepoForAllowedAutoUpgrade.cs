using Credfeto.DotNet.Repo.Tools.Release.Extensions;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Extensions;

// CheckRepoForAllowedAutoUpgrade Looks at (NOT ReleaseConfig.NeverRelease AND ReleaseConfig.AlwaysMatch) OR ReleaseConfig.AllowedAutoUpgrade
public sealed partial class ReleaseConfigExtensionsTests
{

    [Theory]
    [InlineData("git@github.com:credfeto/test1.git")]
    [InlineData("git@github.com:example/auto-upgrade-true.git")]
    [InlineData("git@github.com:example/code-analysis.git")]
    public void CheckRepoForAllowedAutoUpgrade_ReturnsTrue(string repoUrl)
    {
        bool status = this._releaseConfig.CheckRepoForAllowedAutoUpgrade(repoUrl);
        Assert.True(condition: status, userMessage: "Allows auto upgrade");
    }

    [Theory]
    [InlineData("git@github.com:example/auto-upgrade-false.git")]
    public void CheckRepoForAllowedAutoUpgrade_ReturnsFalse(string repoUrl)
    {
        bool status = this._releaseConfig.CheckRepoForAllowedAutoUpgrade(repoUrl);
        Assert.False(condition: status, userMessage: "Should never release");
    }
}