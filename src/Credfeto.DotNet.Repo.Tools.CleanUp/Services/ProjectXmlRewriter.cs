using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Credfeto.DotNet.Repo.Tools.CleanUp.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;

namespace Credfeto.DotNet.Repo.Tools.CleanUp.Services;

public sealed partial class ProjectXmlRewriter : IProjectXmlRewriter
{
    private readonly ILogger<ProjectXmlRewriter> _logger;

    public ProjectXmlRewriter(ILogger<ProjectXmlRewriter> logger)
    {
        this._logger = logger;
    }

    public bool ReOrderPropertyGroups(XmlDocument projectDocument, string filename)
    {
        if (projectDocument.SelectSingleNode("Project") is not XmlElement project)
        {
            return false;
        }

        string before = projectDocument.InnerXml;

        // Only merge/sort runs of combinable PropertyGroups that are not separated by an
        // Import/ImportGroup or a conditional PropertyGroup, as MSBuild evaluates properties
        // in document order and merging across such a boundary would change evaluation order.
        (
            IReadOnlyList<XmlElement> nonCombinablePropertyGroups,
            IReadOnlyList<IReadOnlyList<XmlElement>> combinableRuns
        ) = CollectNonCombinablePropertyGroupsAndCombinableRuns(project);

        foreach (IReadOnlyList<XmlElement> run in combinableRuns)
        {
            this.MergeCombinablePropertyGroups(fileName: filename, combinablePropertyGroups: run);
        }

        this.ReOrderPropertyGroupWithAttributesOrComments(
            filename: filename,
            propertyGroups: nonCombinablePropertyGroups
        );

        string after = projectDocument.InnerXml;

        return !StringComparer.Ordinal.Equals(x: before, y: after);
    }

    // Each PropertyGroup's combinable/non-combinable classification is only ever needed once, so
    // both the non-combinable groups (destined for ReOrderPropertyGroupWithAttributesOrComments) and
    // the combinable runs are partitioned in a single pass over the document, taken before any
    // mutation happens.
    private static (
        IReadOnlyList<XmlElement> NonCombinablePropertyGroups,
        IReadOnlyList<IReadOnlyList<XmlElement>> CombinableRuns
    ) CollectNonCombinablePropertyGroupsAndCombinableRuns(XmlElement project)
    {
        List<XmlElement> nonCombinablePropertyGroups = [];
        List<IReadOnlyList<XmlElement>> combinableRuns = [];
        List<XmlElement> currentRun = [];

        foreach (XmlElement child in project.ChildNodes.OfType<XmlElement>())
        {
            bool isPropertyGroup = IsPropertyGroup(child);

            if (isPropertyGroup && IsCombinableGroup(child))
            {
                currentRun.Add(child);

                continue;
            }

            if (isPropertyGroup)
            {
                nonCombinablePropertyGroups.Add(child);
            }

            if ((isPropertyGroup || IsImport(child)) && currentRun.Count != 0)
            {
                combinableRuns.Add(currentRun);

                currentRun = [];
            }
        }

        if (currentRun.Count != 0)
        {
            combinableRuns.Add(currentRun);
        }

        return (nonCombinablePropertyGroups, combinableRuns);
    }

    private static bool IsImport(XmlElement element)
    {
        return StringComparer.Ordinal.Equals(x: element.Name, y: "Import")
            || StringComparer.Ordinal.Equals(x: element.Name, y: "ImportGroup");
    }

    private static bool IsPropertyGroup(XmlElement element)
    {
        return StringComparer.Ordinal.Equals(x: element.Name, y: "PropertyGroup");
    }

    [SuppressMessage(
        category: "Meziantou.Analyzer",
        checkId: "MA0051: Method is too long",
        Justification = "Needs simplification"
    )]
    public bool ReOrderIncludes(XmlDocument projectDocument, string filename)
    {
        if (projectDocument.SelectSingleNode("Project") is not XmlElement project)
        {
            return false;
        }

        IReadOnlyList<XmlElement> itemGroups =
        [
            .. project
                .ChildNodes.OfType<XmlElement>()
                .Where(n => StringComparer.Ordinal.Equals(x: n.Name, y: "ItemGroup")),
        ];

        string before = projectDocument.InnerXml;

        List<XmlElement> sourceGroups = [];
        Dictionary<string, XmlNode> projectReferences = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, XmlNode> packageReferencesNormal = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, XmlNode> packageReferencesPrivateGroup = new(StringComparer.OrdinalIgnoreCase);

        foreach (XmlElement itemGroup in itemGroups)
        {
            if (itemGroup.HasAttributes)
            {
                this._logger.SkippingGroupWithAttribute(filename);

                continue;
            }

            if (itemGroup.ChildNodes.OfType<XmlNode>().Any(IsComment))
            {
                this._logger.SkippingGroupWithComment(filename);

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
                            this._logger.SkippingGroupWithDuplicatePackage(filename: filename, packageId: packageId);

                            return false;
                        }
                    }
                    else
                    {
                        if (!packageReferencesPrivateGroup.TryAdd(key: packageId, value: reference))
                        {
                            this._logger.SkippingGroupWithDuplicatePackage(filename: filename, packageId: packageId);

                            return false;
                        }
                    }
                }
                else if (StringComparer.Ordinal.Equals(x: reference.Name, y: "ProjectReference"))
                {
                    string projectPath = reference.GetAttribute("Include");

                    if (!projectReferences.TryAdd(key: projectPath, value: reference))
                    {
                        this._logger.SkippingGroupWithDuplicateProject(filename: filename, projectPath: projectPath);

                        return false;
                    }
                }
                else
                {
                    this._logger.SkippingGroupWithUnknownItemType(filename: filename, referenceType: reference.Name);

                    return false;
                }
            }
        }

        // Add in New item groups at the end of the file for each of the types of reference
        AppendReferences(projectDocument: projectDocument, source: projectReferences, project: project);
        AppendReferences(projectDocument: projectDocument, source: packageReferencesNormal, project: project);
        AppendReferences(projectDocument: projectDocument, source: packageReferencesPrivateGroup, project: project);

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

    private void MergeCombinablePropertyGroups(string fileName, IReadOnlyList<XmlElement> combinablePropertyGroups)
    {
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
                if (!orderedChildren.TryAdd(key: child.Name, value: child))
                {
                    this._logger.DuplicateProperty(filename: fileName, propertyName: child.Name);

                    throw new XmlException($"Duplicate property {child.Name} : {fileName}");
                }
            }

            if (targetPropertyGroup != propertyGroup)
            {
                toRemove.Add(propertyGroup);
            }
        }

        if (this.WouldBreakPropertyReferenceOrderLogged(properties: orderedChildren, filename: fileName))
        {
            return;
        }

        ReplaceChildrenInKeyOrder(group: targetPropertyGroup, orderedChildren: orderedChildren);

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
        Justification = "Should be simplified"
    )]
    private void ReOrderPropertyGroupWithAttributesOrComments(string filename, IReadOnlyList<XmlElement> propertyGroups)
    {
        foreach (XmlElement propertyGroup in propertyGroups)
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
                this._logger.SkippingGroupWithComment(filename: filename);

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

                    this._logger.SkippingGroupWithDuplicate(filename: filename, name: name);

                    break;
                }
            }

            if (
                replace && !this.WouldBreakPropertyReferenceOrderLogged(properties: orderedChildren, filename: filename)
            )
            {
                ReplaceChildrenInKeyOrder(group: propertyGroup, orderedChildren: orderedChildren);

                foreach (KeyValuePair<string, string> attribute in attributes)
                {
                    propertyGroup.SetAttribute(name: attribute.Key, value: attribute.Value);
                }
            }
        }
    }

    private static void ReplaceChildrenInKeyOrder(
        XmlElement group,
        IReadOnlyDictionary<string, XmlNode> orderedChildren
    )
    {
        group.RemoveAll();

        foreach (string entryKey in orderedChildren.Keys.Order(comparer: StringComparer.Ordinal))
        {
            XmlNode item = orderedChildren[entryKey];
            group.AppendChild(item);
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

    private bool WouldBreakPropertyReferenceOrderLogged(
        IReadOnlyDictionary<string, XmlNode> properties,
        string filename
    )
    {
        if (!WouldBreakPropertyReferenceOrder(properties))
        {
            return false;
        }

        this._logger.SkippingGroupWithForwardReference(filename);

        return true;
    }

    // Checks whether sorting the given set of same-scope properties into alphabetical order would move
    // a property that references another property in the same set (via $(PropertyName)) above the
    // property it depends on, which would silently change what the reference evaluates to.
    private static bool WouldBreakPropertyReferenceOrder(IReadOnlyDictionary<string, XmlNode> properties)
    {
        foreach ((string propertyName, XmlNode node) in properties)
        {
            foreach (Match match in PropertyReference().Matches(node.InnerText))
            {
                string referencedProperty = match.Groups["name"].Value;

                if (StringComparer.Ordinal.Equals(x: referencedProperty, y: propertyName))
                {
                    // Self-reference, e.g. DefineConstants appending to its own current value
                    continue;
                }

                if (!properties.ContainsKey(referencedProperty))
                {
                    // Defined outside this group/run (e.g. Directory.Build.props) - unaffected by in-file reordering
                    continue;
                }

                if (StringComparer.Ordinal.Compare(x: propertyName, y: referencedProperty) < 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [GeneratedRegex(
        pattern: @"\$\((?<name>\w+)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 5000
    )]
    private static partial Regex PropertyReference();
}
