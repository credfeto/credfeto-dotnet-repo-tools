using Credfeto.DotNet.Repo.Tools.Build.Helpers;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests;

public sealed class FrameWorkSettingsBuilderTests : TestBase
{
    [Theory]
    [InlineData("10.0.100-rc.1.123456.789", true, "9.0.305", false, "10.0.100-rc.1.123456.789", "true")]
    [InlineData("9.0.305", false, "10.0.100-rc.1.123456.789", true, "10.0.100-rc.1.123456.789", "true")]
    [InlineData("10.0.100-rc.1.123456.789", true, "10.0.100-rc.2.123456.789", true, "10.0.100-rc.2.123456.789", "true")]
    [InlineData("10.0.100-rc.2.123456.789", true, "10.0.100-rc.1.123456.789", true, "10.0.100-rc.2.123456.789", "true")]
    [InlineData("10.0.100-rc.2.123456.789", true, "10.0.100", false, "10.0.100", "false")]
    [InlineData(null, true, "9.0.305", false, "9.0.305", "false")]
    [InlineData(null, true, null, false, null, "false")]
    [InlineData("9.0.305", false, null, false, "9.0.305", "false")]
    [InlineData("9.0.305", false, "9.0.304", false, "9.0.305", "false")]
    [InlineData("9.0.305", false, "9.0.306", false, "9.0.306", "false")]
    [InlineData(
        "10.0.100-rc.1.123456.789",
        true,
        "9.0.305-beta.1.123456.789",
        true,
        "10.0.100-rc.1.123456.789",
        "true"
    )]
    [InlineData("10.0.100", false, "9.0.305-rc.1.123456.789", true, "10.0.100", "false")]
    [InlineData(
        "10.0.100-rc.10.123456.789",
        true,
        "10.0.100-rc.9.123456.789",
        true,
        "10.0.100-rc.10.123456.789",
        "true"
    )]
    [InlineData(
        "10.0.100-rc.9.123456.789",
        true,
        "10.0.100-rc.10.123456.789",
        true,
        "10.0.100-rc.10.123456.789",
        "true"
    )]
    [InlineData("10.0.100-preview.7.123456.789", true, "9.0.305", false, "9.0.305", "false")]
    [InlineData(
        "10.0.100-rc.1.123456.789",
        true,
        "10.0.100-preview.7.123456.789",
        true,
        "10.0.100-preview.7.123456.789",
        "true"
    )]
    public static void CheckUpgradeSettings(
        string? repoSdkVersion,
        bool repoAllowPreRelease,
        string? templateSdkVersion,
        bool templateAllowPreRelease,
        string? expectedSdkVersion,
        string expectedAllowPreRelease
    )
    {
        DotNetVersionSettings repo = new(SdkVersion: repoSdkVersion, AllowPreRelease: repoAllowPreRelease);
        DotNetVersionSettings template = new(SdkVersion: templateSdkVersion, AllowPreRelease: templateAllowPreRelease);
        IFrameworkSettings settings = FrameWorkSettingsBuilder.DefineFrameworkSettings(
            repositoryDotNetSettings: repo,
            templateDotNetSettings: template
        );

        Assert.Equal(expected: expectedSdkVersion, actual: settings.DotNetSdkVersion);
        Assert.Equal(expected: expectedAllowPreRelease, actual: settings.DotNetAllowPreReleaseSdk);
    }
}
