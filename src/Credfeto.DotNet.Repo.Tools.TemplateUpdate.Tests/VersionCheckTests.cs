using FunFair.Test.Common;
using NuGet.Versioning;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate.Tests;

public sealed class VersionCheckTests : TestBase
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", true)]
    [InlineData("1.0.0", "1.2.0", true)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("1.0.0", "2.0.0-preview", true)]
    [InlineData("2.0.0", "2.0.0-preview", false)]
    public void IsNewer(string source, string target, bool shouldBeNewer)
    {
        NuGetVersion sourceVersion = new(source);
        NuGetVersion targetVersion = new(target);

        bool actualIsNewer = VersionCheck.IsTargetNewer(sourceVersion: sourceVersion, targetVersion: targetVersion);
        Assert.Equal(shouldBeNewer, actualIsNewer);
    }
}