using System;
using FunFair.BuildCheck.Interfaces;

namespace Credfeto.Dotnet.Repo.Tools.Cmd.Packages;

internal sealed class FrameworkSettings : IFrameworkSettings
{
    public bool IsNullableGloballyEnforced => true;

    public string ProjectImport => Environment.GetEnvironmentVariable("DOTNET_PACK_PROJECT_METADATA_IMPORT") ?? string.Empty;

    public string? DotnetPackable => Environment.GetEnvironmentVariable(variable: "DOTNET_PACKABLE");

    public string? DotnetPublishable => Environment.GetEnvironmentVariable(variable: "DOTNET_PUBLISHABLE");

    public string? DotnetTargetFramework => Environment.GetEnvironmentVariable("DOTNET_CORE_APP_TARGET_FRAMEWORK");

    public string? DotNetSdkVersion => Environment.GetEnvironmentVariable(variable: "DOTNET_CORE_SDK_VERSION");

    public string DotNetAllowPreReleaseSdk => Environment.GetEnvironmentVariable("DOTNET_SDK_ALLOW_PRE_RELEASE") ?? "false";

    public bool XmlDocumentationRequired => StringComparer.InvariantCulture.Equals(Environment.GetEnvironmentVariable("XML_DOCUMENTATION"), y: "true");
}