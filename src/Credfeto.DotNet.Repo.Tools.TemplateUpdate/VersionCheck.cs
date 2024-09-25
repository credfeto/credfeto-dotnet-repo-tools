using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class VersionCheck
{
    public static bool IsTargetNewer(NuGetVersion sourceVersion, NuGetVersion targetVersion)
    {
        return sourceVersion > targetVersion;
    }
}