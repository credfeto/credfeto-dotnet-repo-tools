using System.Diagnostics;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.DotNet.Interfaces.Tests;

public sealed class DotNetVersionSettingsTests : TestBase
{
    [Fact]
    public void MustBeAReadonlyRecordStruct()
    {
        Assert.True(
            typeof(DotNetVersionSettings).IsValueType,
            userMessage: $"{nameof(DotNetVersionSettings)} must be a value type"
        );
    }

    [Fact]
    public void MustBePublic()
    {
        Assert.True(
            typeof(DotNetVersionSettings).IsPublic,
            userMessage: $"{nameof(DotNetVersionSettings)} must be public"
        );
    }

    [Fact]
    public void MustHaveDebuggerDisplayAttribute()
    {
        Assert.NotNull(
            typeof(DotNetVersionSettings).GetCustomAttributes(typeof(DebuggerDisplayAttribute), inherit: false)
        );
    }

    [Fact]
    public void ConstructorSetsSdkVersion()
    {
        const string sdkVersion = "9.0.100";

        DotNetVersionSettings settings = new(
            SdkVersion: sdkVersion,
            AllowPreRelease: false,
            RollForward: "latestMinor"
        );

        Assert.Equal(expected: sdkVersion, actual: settings.SdkVersion);
    }

    [Fact]
    public void ConstructorSetsSdkVersionToNull()
    {
        DotNetVersionSettings settings = new(SdkVersion: null, AllowPreRelease: false, RollForward: "latestMinor");

        Assert.Null(settings.SdkVersion);
    }

    [Fact]
    public void ConstructorSetsAllowPreReleaseToTrue()
    {
        DotNetVersionSettings settings = new(SdkVersion: null, AllowPreRelease: true, RollForward: "latestMinor");

        Assert.True(settings.AllowPreRelease, userMessage: "AllowPreRelease should be true");
    }

    [Fact]
    public void ConstructorSetsAllowPreReleaseToFalse()
    {
        DotNetVersionSettings settings = new(SdkVersion: null, AllowPreRelease: false, RollForward: "latestMinor");

        Assert.False(settings.AllowPreRelease, userMessage: "AllowPreRelease should be false");
    }

    [Fact]
    public void ConstructorSetsRollForward()
    {
        const string rollForward = "latestMajor";

        DotNetVersionSettings settings = new(SdkVersion: null, AllowPreRelease: false, RollForward: rollForward);

        Assert.Equal(expected: rollForward, actual: settings.RollForward);
    }

    [Fact]
    public void TwoSettingsWithSameValuesAreEqual()
    {
        DotNetVersionSettings a = new(SdkVersion: "9.0.100", AllowPreRelease: false, RollForward: "latestMinor");
        DotNetVersionSettings b = new(SdkVersion: "9.0.100", AllowPreRelease: false, RollForward: "latestMinor");

        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoSettingsWithDifferentSdkVersionsAreNotEqual()
    {
        DotNetVersionSettings a = new(SdkVersion: "9.0.100", AllowPreRelease: false, RollForward: "latestMinor");
        DotNetVersionSettings b = new(SdkVersion: "10.0.100", AllowPreRelease: false, RollForward: "latestMinor");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void TwoSettingsWithDifferentAllowPreReleaseAreNotEqual()
    {
        DotNetVersionSettings a = new(SdkVersion: "9.0.100", AllowPreRelease: false, RollForward: "latestMinor");
        DotNetVersionSettings b = new(SdkVersion: "9.0.100", AllowPreRelease: true, RollForward: "latestMinor");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void TwoSettingsWithDifferentRollForwardAreNotEqual()
    {
        DotNetVersionSettings a = new(SdkVersion: "9.0.100", AllowPreRelease: false, RollForward: "latestMinor");
        DotNetVersionSettings b = new(SdkVersion: "9.0.100", AllowPreRelease: false, RollForward: "latestMajor");

        Assert.NotEqual(a, b);
    }
}
