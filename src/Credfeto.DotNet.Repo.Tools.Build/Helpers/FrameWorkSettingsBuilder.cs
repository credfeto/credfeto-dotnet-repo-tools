using System;
using Credfeto.DotNet.Repo.Tools.Build.Services;
using Credfeto.DotNet.Repo.Tools.DotNet.Interfaces;
using FunFair.BuildCheck.Interfaces;
using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.Build.Helpers;

public static class FrameWorkSettingsBuilder
{
    public static IFrameworkSettings DefineFrameworkSettings(in DotNetVersionSettings repositoryDotNetSettings, in DotNetVersionSettings templateDotNetSettings)
    {
        if (string.IsNullOrEmpty(repositoryDotNetSettings.SdkVersion))
        {
            return new FrameworkSettings(templateDotNetSettings);
        }

        if (string.IsNullOrEmpty(templateDotNetSettings.SdkVersion))
        {
            return new FrameworkSettings(repositoryDotNetSettings);
        }

        DotNetVersionSettings dotNetSettings = IsRepositoryFrameworkNewerPreReleaseReleaseCandiate(new(repositoryDotNetSettings.SdkVersion), new(templateDotNetSettings.SdkVersion))
            ? repositoryDotNetSettings
            : templateDotNetSettings;

        return new FrameworkSettings(dotNetSettings);
    }

    private static bool IsRepositoryFrameworkNewerPreReleaseReleaseCandiate(NuGetVersion repositoryFramework, NuGetVersion templateFramework)
    {
        if (repositoryFramework.IsPrerelease)
        {
            if (IsReleaseCandidate(repositoryFramework))
            {
                if (templateFramework.IsPrerelease && IsReleaseCandidate(repositoryFramework))
                {
                    if (repositoryFramework.Version > templateFramework.Version)
                    {
                        // Repo is newer Framework version
                        return true;
                    }

                    if (repositoryFramework.Version == templateFramework.Version)
                    {
                        if (StringComparer.Ordinal.Compare(x: repositoryFramework.Release, y: templateFramework.Release) > 0)
                        {
                            // repo Pre-Release is newer or same as template
                            return true;
                        }
                    }
                }
                else if (repositoryFramework.Version > templateFramework.Version)
                {
                    // Repo is newer Framework version
                    return true;
                }
            }
        }
        else
        {
            if (templateFramework.IsPrerelease && IsReleaseCandidate(templateFramework))
            {
                if (repositoryFramework.Version > templateFramework.Version)
                {
                    return true;
                }
            }
            else if (repositoryFramework.Version > templateFramework.Version)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReleaseCandidate(NuGetVersion repositoryFramework)
    {
        return repositoryFramework.IsPrerelease && repositoryFramework.Release.StartsWith(value: "rc.", comparisonType: StringComparison.Ordinal);
    }
}