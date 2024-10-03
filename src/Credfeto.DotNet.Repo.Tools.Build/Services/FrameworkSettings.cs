using System;
using Credfeto.DotNet.Repo.Tools.Build.Interfaces;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;

namespace Credfeto.DotNet.Repo.Tools.Build.Services;

internal sealed class FrameworkSettings : IFrameworkSettings
{
    public FrameworkSettings(in DotNetVersionSettings dotNetSettings)
    {
        this.DotNetSdkVersion = dotNetSettings.SdkVersion;
        this.DotNetAllowPreReleaseSdk = dotNetSettings.AllowPreRelease
            ? "true"
            : "false";
    }

    public bool IsNullableGloballyEnforced => true;

    public string ProjectImport => Environment.GetEnvironmentVariable("DOTNET_PACK_PROJECT_METADATA_IMPORT") ?? string.Empty;

    public string? DotnetPackable => Environment.GetEnvironmentVariable(variable: "DOTNET_PACKABLE");

    public string? DotnetPublishable => Environment.GetEnvironmentVariable(variable: "DOTNET_PUBLISHABLE");

    public string? DotnetTargetFramework => Environment.GetEnvironmentVariable("DOTNET_CORE_APP_TARGET_FRAMEWORK");

    public string? DotNetSdkVersion { get; }

    public string DotNetAllowPreReleaseSdk { get; }

    public bool XmlDocumentationRequired => StringComparer.InvariantCulture.Equals(Environment.GetEnvironmentVariable("XML_DOCUMENTATION"), y: "true");
}