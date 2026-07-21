using System;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.DotNet.Repo.Tools.Build.Tests.Services;

public sealed class FrameworkSettingsTests : TestBase
{
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Constructor(bool allowPreRelease, string expectedDotNetAllowPreReleaseSdk)
    {
        DotNetVersionSettings dotNetSettings = new(
            SdkVersion: "10.0.100",
            AllowPreRelease: allowPreRelease,
            RollForward: "latestPatch"
        );

        FrameworkSettings settings = new(dotNetSettings);

        Assert.Equal(expected: "10.0.100", actual: settings.DotNetSdkVersion);
        Assert.Equal(expected: expectedDotNetAllowPreReleaseSdk, actual: settings.DotNetAllowPreReleaseSdk);
    }

    [Fact]
    public void IsNullableGloballyEnforcedIsAlwaysTrue()
    {
        FrameworkSettings settings = new(new(SdkVersion: null, AllowPreRelease: false, RollForward: "latestPatch"));

        Assert.True(
            condition: settings.IsNullableGloballyEnforced,
            userMessage: "Nullable must always be globally enforced"
        );
    }

    [Fact]
    public void ProjectImportReturnsEmptyStringWhenEnvironmentVariableNotSet()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_PACK_PROJECT_METADATA_IMPORT",
            value: null,
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Equal(expected: string.Empty, actual: settings.ProjectImport);
            }
        );
    }

    [Fact]
    public void ProjectImportReturnsEnvironmentVariableWhenSet()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_PACK_PROJECT_METADATA_IMPORT",
            value: "Import.props",
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Equal(expected: "Import.props", actual: settings.ProjectImport);
            }
        );
    }

    [Fact]
    public void DotnetPackableReturnsEnvironmentVariable()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_PACKABLE",
            value: "true",
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Equal(expected: "true", actual: settings.DotnetPackable);
            }
        );
    }

    [Fact]
    public void DotnetPackableReturnsNullWhenNotSet()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_PACKABLE",
            value: null,
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Null(settings.DotnetPackable);
            }
        );
    }

    [Fact]
    public void DotnetPublishableReturnsEnvironmentVariable()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_PUBLISHABLE",
            value: "true",
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Equal(expected: "true", actual: settings.DotnetPublishable);
            }
        );
    }

    [Fact]
    public void DotnetPublishableReturnsNullWhenNotSet()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_PUBLISHABLE",
            value: null,
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Null(settings.DotnetPublishable);
            }
        );
    }

    [Fact]
    public void DotnetTargetFrameworkReturnsEnvironmentVariable()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_CORE_APP_TARGET_FRAMEWORK",
            value: "net10.0",
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Equal(expected: "net10.0", actual: settings.DotnetTargetFramework);
            }
        );
    }

    [Fact]
    public void DotnetTargetFrameworkReturnsNullWhenNotSet()
    {
        WithEnvironmentVariable(
            variable: "DOTNET_CORE_APP_TARGET_FRAMEWORK",
            value: null,
            action: static () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Null(settings.DotnetTargetFramework);
            }
        );
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void XmlDocumentationRequired(string? value, bool expected)
    {
        WithEnvironmentVariable(
            variable: "XML_DOCUMENTATION",
            value: value,
            action: () =>
            {
                FrameworkSettings settings = CreateSettings();

                Assert.Equal(expected: expected, actual: settings.XmlDocumentationRequired);
            }
        );
    }

    private static FrameworkSettings CreateSettings()
    {
        return new(new(SdkVersion: "10.0.100", AllowPreRelease: false, RollForward: "latestPatch"));
    }

    private static void WithEnvironmentVariable(string variable, string? value, Action action)
    {
        string? previous = Environment.GetEnvironmentVariable(variable);

        try
        {
            Environment.SetEnvironmentVariable(variable: variable, value: value);

            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable: variable, value: previous);
        }
    }
}
