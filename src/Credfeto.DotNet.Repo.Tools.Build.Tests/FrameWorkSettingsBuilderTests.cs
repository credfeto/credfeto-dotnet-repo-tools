using Credfeto.DotNet.Repo.Tools.Build.Helpers;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests;

public sealed class FrameWorkSettingsBuilderTests : TestBase
{
    [Theory]
    [InlineData("10.0.100-rc.1.123456.789", true, "latestPatch", "9.0.305", false, "latestPatch", "10.0.100-rc.1.123456.789", "true")]
    [InlineData("9.0.305", false, "latestPatch", "10.0.100-rc.1.123456.789", true, "latestPatch", "10.0.100-rc.1.123456.789", "true")]
    [InlineData("10.0.100-rc.1.123456.789", true, "latestPatch", "10.0.100-rc.2.123456.789", true, "latestPatch", "10.0.100-rc.2.123456.789", "true")]
    [InlineData("10.0.100-rc.2.123456.789", true, "latestPatch", "10.0.100-rc.1.123456.789", true, "latestPatch", "10.0.100-rc.2.123456.789", "true")]
    [InlineData("10.0.100-rc.2.123456.789", true, "latestPatch", "10.0.100", false, "latestPatch", "10.0.100", "false")]
    [InlineData(null, true, "latestPatch", "9.0.305", false, "latestPatch", "9.0.305", "false")]
    [InlineData(null, true, "latestPatch", null, false, "latestPatch", null, "false")]
    [InlineData("9.0.305", false, "latestPatch", null, false, "latestPatch", "9.0.305", "false")]
    [InlineData("9.0.305", false, "latestPatch", "9.0.304", false, "latestPatch", "9.0.305", "false")]
    [InlineData("9.0.305", false, "latestPatch", "9.0.306", false, "latestPatch", "9.0.306", "false")]
    public void CheckUpgradeSettings(string? repoSdkVersion,
                                     bool repoAllowPreRelease,
                                     string repoRollForward,
                                     string? templateSdkVersion,
                                     bool templateAllowPreRelease,
                                     string templateRollForward,
                                     string? expectedSdkVersion,
                                     string expectedAllowPreRelease)
    {
        DotNetVersionSettings repo = new(SdkVersion: repoSdkVersion, AllowPreRelease: repoAllowPreRelease, RollForward: repoRollForward);
        DotNetVersionSettings template = new(SdkVersion: templateSdkVersion, AllowPreRelease: templateAllowPreRelease, RollForward: templateRollForward);
        IFrameworkSettings settings = FrameWorkSettingsBuilder.DefineFrameworkSettings(repositoryDotNetSettings: repo, templateDotNetSettings: template);

        Assert.Equal(expected: expectedSdkVersion, actual: settings.DotNetSdkVersion);
        Assert.Equal(expected: expectedAllowPreRelease, actual: settings.DotNetAllowPreReleaseSdk);
    }
}