using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public interface IProjectXmlRewriter
{
    bool ReOrderPropertyGroups(XmlDocument projectDocument, string filename);

    bool ReOrderIncludes(XmlDocument projectDocument, string filename);
}
