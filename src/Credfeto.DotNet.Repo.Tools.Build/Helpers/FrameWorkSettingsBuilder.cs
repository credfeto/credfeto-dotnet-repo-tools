using System;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.Build.Helpers;

public static class FrameWorkSettingsBuilder
{
    public static IFrameworkSettings DefineFrameworkSettings(
        in DotNetVersionSettings repositoryDotNetSettings,
        in DotNetVersionSettings templateDotNetSettings
    )
    {
        if (string.IsNullOrEmpty(repositoryDotNetSettings.SdkVersion))
        {
            return new FrameworkSettings(templateDotNetSettings);
        }

        if (string.IsNullOrEmpty(templateDotNetSettings.SdkVersion))
        {
            return new FrameworkSettings(repositoryDotNetSettings);
        }

        DotNetVersionSettings dotNetSettings = IsRepositoryFrameworkNewerPreReleaseReleaseCandiate(
            new(repositoryDotNetSettings.SdkVersion),
            new(templateDotNetSettings.SdkVersion)
        )
            ? repositoryDotNetSettings
            : templateDotNetSettings;

        return new FrameworkSettings(dotNetSettings);
    }

    private static bool IsRepositoryFrameworkNewerPreReleaseReleaseCandiate(
        NuGetVersion repositoryFramework,
        NuGetVersion templateFramework
    )
    {
        bool repositoryIsReleaseCandidate = IsReleaseCandidate(repositoryFramework);

        if (repositoryFramework.IsPrerelease && !repositoryIsReleaseCandidate)
        {
            return false;
        }

        if (repositoryIsReleaseCandidate && IsReleaseCandidate(templateFramework))
        {
            // Repo is newer Framework version or newer Pre-Release
            return repositoryFramework > templateFramework;
        }

        // Repo is newer Framework version
        return repositoryFramework.Version > templateFramework.Version;
    }

    private static bool IsReleaseCandidate(NuGetVersion repositoryFramework)
    {
        return repositoryFramework.IsPrerelease
            && repositoryFramework.Release.StartsWith(value: "rc.", comparisonType: StringComparison.Ordinal);
    }
}
