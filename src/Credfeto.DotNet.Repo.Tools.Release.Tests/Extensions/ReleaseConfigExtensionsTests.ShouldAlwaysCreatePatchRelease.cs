using Credfeto.DotNet.Repo.Tools.Release.Extensions;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Release.Tests.Extensions;

// ShouldAlwaysCreatePatchRelease Looks at (NOT ReleaseConfig.NeverRelease AND ReleaseConfig.AlwaysMatch)
public sealed partial class ReleaseConfigExtensionsTests
{
    [Theory]
    [InlineData("git@github.com:credfeto/test1.git")]
    [InlineData("git@github.com:example/always-match.git")]
    public void ShouldAlwaysCreatePatchRelease_ReturnsTrue(string repoUrl)
    {
        bool status = this._releaseConfig.ShouldAlwaysCreatePatchRelease(repoUrl);
        Assert.True(condition: status, userMessage: "Should always create patch release without further checks");
    }

    [Theory]
    [InlineData("git@github.com:example/cs-template.git")]
    [InlineData("git@github.com:example/never-release.git")]
    [InlineData("git@github.com:example/code-analysis.git")]
    [InlineData("git@github.com:example/test1.git")]
    public void ShouldAlwaysCreatePatchRelease_ReturnsFalse(string repoUrl)
    {
        bool status = this._releaseConfig.ShouldAlwaysCreatePatchRelease(repoUrl);
        Assert.False(condition: status, userMessage: "Should not always create patch release without further checks");
    }
}