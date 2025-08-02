using System.Xml;
using Credfeto.DotNet.Repo.Tools.Dependencies.Models;

namespace Credfeto.DotNet.Repo.Tools.Dependencies.Helpers;

internal static class ProjectExtractor
{
    public static ProjectReference? ExtractProjectReference(XmlElement node)
    {
        string relativeFileName = node.GetAttribute("Include");

        if (string.IsNullOrEmpty(relativeFileName))
        {
            return null;
        }

        return new(relativeFileName);
    }

    public static FileProjectReference? ExtractProjectReference(string fileName, XmlElement node)
    {
        ProjectReference? projectReference = ExtractProjectReference(node);

        return projectReference?.ToFileProjectReference(fileName);
    }
}