using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed class ProjectXmlRewriter : IProjectXmlRewriter
{
    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "TODO just comments"
    )]
    public bool ReOrderPropertyGroups(XmlDocument projectDocument, string filename)
    {
        if (projectDocument.SelectSingleNode("Project") is not XmlElement project)
        {
            return false;
        }

        XmlNodeList? propertyGroups = project.SelectNodes("PropertyGroup");

        if (propertyGroups is null)
        {
            return false;
        }

        string before = projectDocument.InnerXml;

        MergeProprtiesOfMultipleGroups(propertyGroups: propertyGroups);

        ReOrderPropertyGroupWithAttributesOrComments(
            filename: filename,
            propertyGroups: propertyGroups
        );

        string after = projectDocument.InnerXml;

        return !StringComparer.Ordinal.Equals(x: before, y: after);
    }

    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "TODO just comments"
    )]
    public bool ReOrderIncludes(XmlDocument projectDocument, string filename)
    {
        if (projectDocument.SelectSingleNode("Project") is not XmlElement project)
        {
            return false;
        }

        XmlNodeList? itemGroups = project.SelectNodes("ItemGroup");

        if (itemGroups is null)
        {
            return false;
        }

        string before = projectDocument.InnerXml;

        List<XmlElement> sourceGroups = [];
        Dictionary<string, XmlNode> projectReferences = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, XmlNode> packageReferencesNormal = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, XmlNode> packageReferencesPrivateGroup = new(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (XmlElement itemGroup in itemGroups)
        {
            if (itemGroup.HasAttributes)
            {
                Log($"SKIPPING GROUP AS Found Attribute : {filename}");

                continue;
            }

            if (itemGroup.ChildNodes.OfType<XmlNode>().Any(IsComment))
            {
                Log($"SKIPPING GROUP AS Found Comment : {filename}");

                continue;
            }

            sourceGroups.Add(itemGroup);

            foreach (XmlElement reference in itemGroup.ChildNodes)
            {
                if (StringComparer.Ordinal.Equals(x: reference.Name, y: "PackageReference"))
                {
                    string packageId = reference.GetAttribute("Include");
                    string privateAssets = reference.GetAttribute("PrivateAssets");

                    if (string.IsNullOrEmpty(privateAssets))
                    {
                        if (!packageReferencesNormal.TryAdd(key: packageId, value: reference))
                        {
                            Log($"SKIPPING GROUP AS Found Duplicate item {packageId} : {filename}");

                            return false;
                        }
                    }
                    else
                    {
                        if (!packageReferencesPrivateGroup.TryAdd(key: packageId, value: reference))
                        {
                            Log($"SKIPPING GROUP AS Found Duplicate item {packageId} : {filename}");

                            return false;
                        }
                    }
                }
                else if (StringComparer.Ordinal.Equals(x: reference.Name, y: "ProjectReference"))
                {
                    string projectPath = reference.GetAttribute("Include");

                    if (!projectReferences.TryAdd(key: projectPath, value: reference))
                    {
                        Log($"SKIPPING GROUP AS Found Duplicate item {projectPath} : {filename}");

                        return false;
                    }
                }
                else
                {
                    Log($"SKIPPING GROUP AS Found Unknown item {reference.Name} : {filename}");

                    return false;
                }
            }
        }

        // Add in New item groups at the end of the file for each of the types of reference
        AppendReferences(
            projectDocument: projectDocument,
            source: projectReferences,
            project: project
        );
        AppendReferences(
            projectDocument: projectDocument,
            source: packageReferencesNormal,
            project: project
        );
        AppendReferences(
            projectDocument: projectDocument,
            source: packageReferencesPrivateGroup,
            project: project
        );

        RemoveNodes(sourceGroups);

        string after = projectDocument.InnerXml;

        return !StringComparer.Ordinal.Equals(x: before, y: after);
    }

    private static void AppendReferences(
        XmlDocument projectDocument,
        Dictionary<string, XmlNode> source,
        XmlElement project
    )
    {
        if (source.Count == 0)
        {
            return;
        }

        XmlElement itemGroup = projectDocument.CreateElement("ItemGroup");

        foreach (
            (string _, XmlNode node) in source.OrderBy(
                keySelector: x => x.Key,
                comparer: StringComparer.OrdinalIgnoreCase
            )
        )
        {
            itemGroup.AppendChild(node);
        }

        project.AppendChild(itemGroup);
    }

    private static void RemoveNodes(List<XmlElement> toRemove)
    {
        foreach (XmlElement item in toRemove)
        {
            // ! Should always have a parent here
            XmlNode parent = item.ParentNode!;

            parent.RemoveChild(item);
        }
    }

    private static void MergeProprtiesOfMultipleGroups(XmlNodeList propertyGroups)
    {
        IReadOnlyList<XmlElement> combinablePropertyGroups =
        [
            .. propertyGroups.OfType<XmlElement>().Where(IsCombinableGroup),
        ];

        XmlElement? targetPropertyGroup = combinablePropertyGroups.FirstOrDefault();

        if (targetPropertyGroup is null)
        {
            return;
        }

        List<XmlElement> toRemove = [];
        Dictionary<string, XmlNode> orderedChildren = new(StringComparer.Ordinal);

        foreach (XmlElement propertyGroup in combinablePropertyGroups)
        {
            XmlNodeList children = propertyGroup.ChildNodes;

            foreach (XmlElement child in children)
            {
                orderedChildren.Add(key: child.Name, value: child);
            }

            if (targetPropertyGroup != propertyGroup)
            {
                toRemove.Add(propertyGroup);
            }
        }

        // Empty the target property group
        targetPropertyGroup.RemoveAll();

        // Add the children we've added to the target property group
        foreach (string entryKey in orderedChildren.Keys.Order(comparer: StringComparer.Ordinal))
        {
            XmlNode item = orderedChildren[entryKey];
            targetPropertyGroup.AppendChild(item);
        }

        // remove the old groups
        RemoveNodes(toRemove);
    }

    private static bool IsCombinableGroup(XmlElement propertyGroup)
    {
        if (propertyGroup.HasAttributes)
        {
            return false;
        }

        XmlNodeList children = propertyGroup.ChildNodes;

        HashSet<string> childNames = new(StringComparer.Ordinal);

        foreach (XmlNode child in children)
        {
            if (IsComment(child))
            {
                return false;
            }

            if (IsDefineConstants(child))
            {
                return false;
            }

            if (!childNames.Add(child.Name))
            {
                // Has a duplicate name
                return false;
            }
        }

        return true;
    }

    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "TODO just comments"
    )]
    private static void ReOrderPropertyGroupWithAttributesOrComments(
        string filename,
        XmlNodeList propertyGroups
    )
    {
        IReadOnlyList<XmlElement> nonCombinablePropertyGroups =
        [
            .. propertyGroups.OfType<XmlElement>().Where(ph => !IsCombinableGroup(ph)),
        ];

        foreach (XmlElement propertyGroup in nonCombinablePropertyGroups)
        {
            Dictionary<string, string> attributes = new(StringComparer.Ordinal);

            foreach (XmlAttribute attribute in propertyGroup.Attributes)
            {
                string attValue = propertyGroup.GetAttribute(attribute.Name);
                attributes[attribute.Name] = attValue;
            }

            XmlNodeList children = propertyGroup.ChildNodes;

            if (children.OfType<XmlNode>().Any(IsComment))
            {
                Log(message: $"{filename} SKIPPING GROUP AS Found Comment");

                continue;
            }

            Dictionary<string, XmlNode> orderedChildren = new(StringComparer.Ordinal);
            bool replace = true;

            foreach (XmlElement child in children)
            {
                if (IsDefineConstants(child))
                {
                    // Skip DefineConstants as they can be added many times
                    replace = false;

                    break;
                }

                string name = child.Name;

                if (!orderedChildren.TryAdd(key: name, value: child))
                {
                    replace = false;

                    Log(message: $"{filename} SKIPPING GROUP AS Found Duplicate item {name}");

                    break;
                }
            }

            if (replace)
            {
                propertyGroup.RemoveAll();

                foreach (
                    string entryKey in orderedChildren.Keys.Order(comparer: StringComparer.Ordinal)
                )
                {
                    XmlNode item = orderedChildren[entryKey];
                    propertyGroup.AppendChild(item);
                }

                foreach (KeyValuePair<string, string> attribute in attributes)
                {
                    propertyGroup.SetAttribute(name: attribute.Key, value: attribute.Value);
                }
            }
        }
    }

    private static bool IsDefineConstants(XmlNode node)
    {
        return StringComparer.Ordinal.Equals(x: node.Name, y: "DefineConstants");
    }

    private static bool IsComment(XmlNode node)
    {
        return node.NodeType == XmlNodeType.Comment;
    }

    private static void Log(string message)
    {
        Debug.WriteLine(message);
    }
}
