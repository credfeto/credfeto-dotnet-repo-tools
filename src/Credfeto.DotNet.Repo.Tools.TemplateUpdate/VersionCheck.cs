using NuGet.Versioning;

namespace Credfeto.DotNet.Repo.Tools.TemplateUpdate;

public static class VersionCheck
{
    public static bool IsDotNetSdkTargetNewer(
        NuGetVersion sourceVersion,
        NuGetVersion targetVersion
    )
    {
        // Shouldn't need to do more than this, but having it as a method like this allows for future configuration
        return targetVersion > sourceVersion;
    }
}
