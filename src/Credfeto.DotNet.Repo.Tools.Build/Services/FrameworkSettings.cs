using System;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

internal sealed class FrameworkSettings : IFrameworkSettings
{
    private readonly DotNetVersionSettings _dotNetSettings;

    public FrameworkSettings(in DotNetVersionSettings dotNetSettings)
    {
        this._dotNetSettings = dotNetSettings;
    }

    public bool IsNullableGloballyEnforced => true;

    public string ProjectImport => Environment.GetEnvironmentVariable("DOTNET_PACK_PROJECT_METADATA_IMPORT") ?? string.Empty;

    public string? DotnetPackable => Environment.GetEnvironmentVariable(variable: "DOTNET_PACKABLE");

    public string? DotnetPublishable => Environment.GetEnvironmentVariable(variable: "DOTNET_PUBLISHABLE");

    public string? DotnetTargetFramework => Environment.GetEnvironmentVariable("DOTNET_CORE_APP_TARGET_FRAMEWORK");

    public string? DotNetSdkVersion => this._dotNetSettings.SdkVersion;

    public string DotNetAllowPreReleaseSdk =>
        this._dotNetSettings.AllowPreRelease
            ? "true"
            : "false";

    public bool XmlDocumentationRequired => StringComparer.InvariantCulture.Equals(Environment.GetEnvironmentVariable("XML_DOCUMENTATION"), y: "true");
}