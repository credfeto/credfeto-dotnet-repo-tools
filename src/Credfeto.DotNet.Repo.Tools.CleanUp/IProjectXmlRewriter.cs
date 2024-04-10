using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.CleanUp;

public interface IProjectXmlRewriter
{
    void ReOrderPropertyGroups(XmlDocument project, string filename);

    void ReOrderIncludes(XmlDocument project);
}