using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public interface IProjectXmlRewriter
{
    void ReOrderPropertyGroups(XmlDocument projectDocument, string filename);

    void ReOrderIncludes(XmlDocument projectDocument, string filename);
}