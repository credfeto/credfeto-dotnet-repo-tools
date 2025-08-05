using System.Collections.Generic;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;
using Credfeto.DotNet.Repo.Tools.Dependencies.Services;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;

internal static class PackageExtractor
{
    public static PackageReference? ExtractPackageReference(XmlElement node)
    {
        string packageId = node.GetAttribute("Include");

        if (string.IsNullOrEmpty(packageId))
        {
            return null;
        }

        XmlNode? privateAssetsNode = node.SelectSingleNode("PrivateAssets");

        if (privateAssetsNode is not null)
        {
            return null;
        }

        string privateAssets = node.GetAttribute("PrivateAssets");

        if (!string.IsNullOrEmpty(privateAssets))
        {
            return null;
        }

        string version = node.GetAttribute("Version");

        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        return new(PackageId: packageId, Version: version);
    }

    public static FilePackageReference? ExtractPackageReference(
        ReferenceConfig config,
        XmlElement node,
        List<string> allPackageIds,
        string baseDir
    )
    {
        PackageReference? packageReference = ExtractPackageReference(node);

        if (packageReference is null)
        {
            return null;
        }

        return config.IsDoNotRemovePackage(packageId: packageReference.PackageId, allPackageIds: allPackageIds)
            ? null
            : packageReference.ToFilePackageReference(baseDir);
    }
}
