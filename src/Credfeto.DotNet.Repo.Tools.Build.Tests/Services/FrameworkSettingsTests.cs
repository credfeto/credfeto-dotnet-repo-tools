using Credfeto.DotNet.Repo.Tools.Build.Helpers;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using FunFair.Test.Common;
using FunFair.Test.Common.Helpers;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Services;

public sealed class FrameworkSettingsTests : TestBase
{
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void ExposesSdkVersionAndAllowPreRelease(bool allowPreRelease, string expectedDotNetAllowPreReleaseSdk)
    {
        DotNetVersionSettings dotNetSettings = new(
            SdkVersion: "10.0.100",
            AllowPreRelease: allowPreRelease,
            RollForward: "latestPatch"
        );

        IFrameworkSettings settings = CreateSettings(dotNetSettings);

        Assert.Equal(expected: "10.0.100", actual: settings.DotNetSdkVersion);
        Assert.Equal(expected: expectedDotNetAllowPreReleaseSdk, actual: settings.DotNetAllowPreReleaseSdk);
    }

    [Fact]
    public void IsNullableGloballyEnforcedIsAlwaysTrue()
    {
        IFrameworkSettings settings = CreateSettings(
            new(SdkVersion: "10.0.100", AllowPreRelease: false, RollForward: "latestPatch")
        );

        Assert.True(
            condition: settings.IsNullableGloballyEnforced,
            userMessage: "Nullable must always be globally enforced"
        );
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Import.props", "Import.props")]
    public void ProjectImport(string? value, string expected)
    {
        using (new EnvironmentVariableScope(variableName: "DOTNET_PACK_PROJECT_METADATA_IMPORT", value: value))
        {
            IFrameworkSettings settings = CreateSettings();

            Assert.Equal(expected: expected, actual: settings.ProjectImport);
        }
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData(null, null)]
    public void DotnetPackable(string? value, string? expected)
    {
        using (new EnvironmentVariableScope(variableName: "DOTNET_PACKABLE", value: value))
        {
            IFrameworkSettings settings = CreateSettings();

            Assert.Equal(expected: expected, actual: settings.DotnetPackable);
        }
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData(null, null)]
    public void DotnetPublishable(string? value, string? expected)
    {
        using (new EnvironmentVariableScope(variableName: "DOTNET_PUBLISHABLE", value: value))
        {
            IFrameworkSettings settings = CreateSettings();

            Assert.Equal(expected: expected, actual: settings.DotnetPublishable);
        }
    }

    [Theory]
    [InlineData("net10.0", "net10.0")]
    [InlineData(null, null)]
    public void DotnetTargetFramework(string? value, string? expected)
    {
        using (new EnvironmentVariableScope(variableName: "DOTNET_CORE_APP_TARGET_FRAMEWORK", value: value))
        {
            IFrameworkSettings settings = CreateSettings();

            Assert.Equal(expected: expected, actual: settings.DotnetTargetFramework);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void XmlDocumentationRequired(string? value, bool expected)
    {
        using (new EnvironmentVariableScope(variableName: "XML_DOCUMENTATION", value: value))
        {
            IFrameworkSettings settings = CreateSettings();

            Assert.Equal(expected: expected, actual: settings.XmlDocumentationRequired);
        }
    }

    private static IFrameworkSettings CreateSettings()
    {
        return CreateSettings(new(SdkVersion: "10.0.100", AllowPreRelease: false, RollForward: "latestPatch"));
    }

    private static IFrameworkSettings CreateSettings(in DotNetVersionSettings dotNetSettings)
    {
        return FrameWorkSettingsBuilder.DefineFrameworkSettings(
            repositoryDotNetSettings: dotNetSettings,
            templateDotNetSettings: new(SdkVersion: null, AllowPreRelease: false, RollForward: "latestPatch")
        );
    }
}
